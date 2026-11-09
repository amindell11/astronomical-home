using System;
using System.Collections;
using System.Collections.Generic;
using Balance;
using Objectives;
using Ships;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Services.Objectives;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's repair stations: the authored body plus copies of it, as many as the feel asks for. At setup
    /// each picks its own point away from the sector's start, spaced from the others; the asteroid field reads
    /// those points and leaves a clearing of the station's clear radius. On the first tick each shows, marked as a find (its minimap marker in range, an edge pointer out of it) until it goes dark.
    /// While the hero sits in one it restores hull at the feel's rate from that station's supply, which drains
    /// only by what it heals, and the station goes dark (glow and minimap marker off) when its supply is spent,
    /// or when the sector's hunt raises its alarm, losing whatever supply is left. A sector with no hunt keeps
    /// them live. The arrival events are plain C#, raised once per station on its first arrival.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class RepairStation : SectorModule
    {
        [Tooltip("Repair rate, supply and station count.")]
        [Stat, SerializeField] private RepairStationFeelProfile feel;
        [Tooltip("The fly-in trigger, on or under the body.")]
        [SerializeField] private ExtractionZone zone;
        [Tooltip("The model, zone and lit parts; copied per station and hidden until placed.")]
        [SerializeField] private GameObject body;
        [Tooltip("The glow and the minimap marker, under the body; hidden when the station goes dark.")]
        [SerializeField] private GameObject lit;
        [Tooltip("The sector's opening hunt, read for its alarm, which darkens every station; unset for none.")]
        [SerializeField] private SectorHunt hunt;
        [Tooltip("The find icon its minimap marker and edge pointer show.")]
        [SerializeField] private Sprite icon;

        [Header("Placement")]
        [Tooltip("Plane distances from the hero's start the stations are placed between.")]
        [SerializeField, Min(0f)] private float distanceMin = 120f;
        [SerializeField, Min(0f)] private float distanceMax = 180f;
        [Tooltip("Least plane distance between two of the sector's stations.")]
        [SerializeField, Min(0f)] private float spacing = 60f;
        [Tooltip("Plane radius around each station the asteroid field leaves empty.")]
        [SerializeField, Min(0f)] private float clearRadius = 35f;
        [Tooltip("Random points tried; the last one tried is used when none is spaced.")]
        [SerializeField, Min(1)] private int placementAttempts = 8;

        private readonly List<Station> stations = new();
        private readonly List<GameObject> copies = new();
        private Ship hero;
        private IObjectiveService objectives;
        private int sectorIndex;
        private bool shown;

        /// <summary>The hero reached a station for the first time: station kind, sector index.</summary>
        public event Action<StationKind, int> Arrived;

        /// <summary>The hero claimed a find, on its first arrival: station kind.</summary>
        public event Action<StationKind> FindClaimed;

        public StationKind Kind => StationKind.Repair;

        public IReadOnlyList<Station> Stations => stations;

        public float ClearRadius => clearRadius;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel || !zone || !body || !lit || !icon || ctx.Objectives == null || !ctx.Sector
                || !zone.transform.IsChildOf(body.transform) || !lit.transform.IsChildOf(body.transform))
                throw new InvalidOperationException(
                    $"RepairStation on '{name}' needs its feel profile, icon, objective service, sector, body, and a zone and lit parts under the body.");

            hero = ctx.Hero;
            objectives = ctx.Objectives;
            sectorIndex = ctx.SectorIndex;
            shown = false;
            ClearStations();
            body.SetActive(false);
            var taken = new List<Vector3>(feel.stationsPerSector);
            for (var i = 0; i < feel.stationsPerSector; i++)
            {
                var station = i == 0 ? new Station(body, zone, lit) : Copy();
                var point = SpacedPoint.Around(ctx.Sector.StartPoint, distanceMin, distanceMax, placementAttempts,
                    taken, spacing);
                taken.Add(point);
                station.Reset(feel.supply, hero, point);
                stations.Add(station);
            }
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            ClearStations();
            body.SetActive(false);
            hero = null;
            objectives = null;
            yield break;
        }

        private void Update()
        {
            if (!hero || !hero.gameObject.activeInHierarchy) return;
            Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            if (!shown)
            {
                Show();
                return;
            }

            var alarmed = hunt && hunt.Alarmed;
            foreach (var station in stations)
            {
                if (station.Spent) continue;
                if (alarmed)
                {
                    station.GoDark();
                    continue;
                }
                if (!station.Zone.IsPlayerInZone) continue;

                if (!station.HasArrived)
                {
                    station.HasArrived = true;
                    Arrived?.Invoke(Kind, sectorIndex);
                    FindClaimed?.Invoke(Kind);
                }
                station.Repair(hero, feel.repairPerSecond * deltaTime);
            }
        }

        private void Show()
        {
            foreach (var station in stations)
                station.Show(objectives.MarkFind(station.Body.transform, icon));
            shown = true;
        }

        private Station Copy()
        {
            var copy = Instantiate(body, body.transform.parent);
            copies.Add(copy);
            return new Station(copy, Counterpart(zone.transform, copy.transform).GetComponent<ExtractionZone>(),
                Counterpart(lit.transform, copy.transform).gameObject);
        }

        private Transform Counterpart(Transform part, Transform copy) =>
            part == body.transform ? copy : Counterpart(part.parent, copy).GetChild(part.GetSiblingIndex());

        private void ClearStations()
        {
            foreach (var station in stations)
            {
                station.Zone.Disarm();
                station.Unmark();
            }
            stations.Clear();
            foreach (var copy in copies)
            {
                copy.SetActive(false);
                Destroy(copy);
            }
            copies.Clear();
        }

        /// <summary>One placed station of the sector's set, with its own supply.</summary>
        public sealed class Station
        {
            internal readonly GameObject Body;
            internal readonly ExtractionZone Zone;
            internal readonly GameObject Lit;
            internal bool HasArrived;
            private FindMarker find;

            internal Station(GameObject body, ExtractionZone zone, GameObject lit)
            {
                Body = body;
                Zone = zone;
                Lit = lit;
            }

            /// <summary>Hull left to give, as a fraction of a full hull.</summary>
            public float SupplyLeft { get; private set; }

            public bool Spent => SupplyLeft <= 0f;

            /// <summary>The point picked at setup; the body moves there when it shows.</summary>
            public Vector3 Position { get; private set; }

            internal void Reset(float supply, Ship hero, Vector3 position)
            {
                Position = position;
                SupplyLeft = supply;
                HasArrived = false;
                Zone.BindPlayer(hero ? hero.Body : null);
                Zone.Disarm();
                Body.SetActive(false);
                Lit.SetActive(true);
            }

            internal void Show(FindMarker marker)
            {
                Body.transform.position = Position;
                Body.SetActive(true);
                Zone.Arm();
                find = marker;
            }

            internal void Repair(Ship hero, float amount)
            {
                var hull = hero.Damage.Health;
                var heal = Mathf.Min(amount, SupplyLeft, 1f - hull.Pct);
                if (heal <= 0f) return;

                hull.RestoreFraction(heal);
                SupplyLeft -= heal;
                if (Spent) GoDark();
            }

            internal void GoDark()
            {
                SupplyLeft = 0f;
                Zone.Disarm();
                Lit.SetActive(false);
                Unmark();
            }

            internal void Unmark()
            {
                find?.Close();
                find = null;
            }
        }

#if UNITY_EDITOR
        internal void Configure(RepairStationFeelProfile feel, float distance, SectorHunt hunt = null,
            float spacing = 0f)
        {
            this.feel = feel;
            this.hunt = hunt;
            this.spacing = spacing;
            distanceMin = distanceMax = distance;
        }
#endif
    }
}
