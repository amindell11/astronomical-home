using System;
using System.Collections;
using System.Collections.Generic;
using Objectives;
using Ships;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Services.Objectives;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's stations of one kind: the authored body plus copies of it, as many as the kind asks for. At
    /// setup each picks its own point away from the sector's start, spaced from the others and from other
    /// kinds' stations, which pick first; the asteroid field reads those points and leaves a clearing of the clear
    /// radius. On the first tick each shows, marked as a find, until it goes dark: when its supply is spent, or
    /// when the sector's hunt raises its alarm, losing whatever supply is left. A sector with no hunt keeps them
    /// live. The arrival events are plain C#, raised once per station on its first arrival. A kind supplies only
    /// its effect: what a live station gives the hero sitting in it, drawn from that station's supply.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public abstract class FindStation : SectorModule
    {
        [Tooltip("The model with its zone and lit parts; copied per station and hidden until placed.")]
        [SerializeField] private FindStationBody body;
        [Tooltip("The sector's opening hunt, read for its alarm, which darkens every live station; unset for none.")]
        [SerializeField] private SectorHunt hunt;
        [Tooltip("Other kinds' stations, read at setup for their points so none of these lands beside one.")]
        [SerializeField] private FindStation[] spacedFrom = { };
        [Tooltip("The find icon its minimap marker and edge pointer show.")]
        [SerializeField] private Sprite icon;

        [Header("Placement")]
        [Tooltip("Plane distances from the hero's start the stations are placed between.")]
        [SerializeField, Min(0f)] private float distanceMin = 120f;
        [SerializeField, Min(0f)] private float distanceMax = 180f;
        [Tooltip("Least plane distance from a station to another of its kind or of a spaced-from kind.")]
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

        public abstract StationKind Kind { get; }

        public IReadOnlyList<Station> Stations => stations;

        public float ClearRadius => clearRadius;

        protected int SectorIndex => sectorIndex;

        protected abstract int StationsPerSector { get; }

        /// <summary>What each station holds to give when it is placed.</summary>
        protected abstract float Supply { get; }

        /// <summary>The hero flew into a live station this tick, the first time or back again, before its visit.</summary>
        protected virtual void Enter(Station station) { }

        /// <summary>The hero sits in a live station this tick, after its arrival events.</summary>
        protected abstract void Visit(Station station, Ship hero, float deltaTime);

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!body || !body.Zone || !body.Lit || !icon || ctx.Objectives == null || !ctx.Sector
                || !body.Zone.transform.IsChildOf(body.transform) || !body.Lit.transform.IsChildOf(body.transform))
                throw new InvalidOperationException(
                    $"{GetType().Name} on '{name}' needs its icon, objective service, sector, body, and a zone and lit parts under the body.");
            if (Array.Exists(spacedFrom, other => !other))
                throw new InvalidOperationException($"{GetType().Name} on '{name}' has an empty spaced-from entry.");

            hero = ctx.Hero;
            objectives = ctx.Objectives;
            sectorIndex = ctx.SectorIndex;
            shown = false;
            ClearStations();
            body.gameObject.SetActive(false);
            var taken = new List<Vector3>();
            foreach (var kind in spacedFrom)
                foreach (var other in kind.Stations)
                    taken.Add(other.Position);
            for (var i = 0; i < StationsPerSector; i++)
            {
                var station = i == 0 ? new Station(body) : Copy();
                var point = SpacedPoint.Around(ctx.Sector.StartPoint, distanceMin, distanceMax, placementAttempts,
                    taken, spacing);
                taken.Add(point);
                station.Reset(Supply, hero, point);
                stations.Add(station);
            }
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            ClearStations();
            body.gameObject.SetActive(false);
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
                var entered = station.Zone.IsPlayerInZone && !station.Inside;
                station.Inside = station.Zone.IsPlayerInZone;
                if (!station.Inside) continue;

                if (!station.HasArrived)
                {
                    station.HasArrived = true;
                    Arrived?.Invoke(Kind, sectorIndex);
                    FindClaimed?.Invoke(Kind);
                }
                if (entered) Enter(station);
                Visit(station, hero, deltaTime);
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
            copies.Add(copy.gameObject);
            return new Station(copy);
        }

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
            internal bool Inside;
            private FindMarker find;

            internal Station(FindStationBody body)
            {
                Body = body.gameObject;
                Zone = body.Zone;
                Lit = body.Lit;
            }

            /// <summary>What the station has left to give, in its kind's unit.</summary>
            public float SupplyLeft { get; private set; }

            /// <summary>Drawn to nothing, or darkened by the alarm.</summary>
            public bool Spent => SupplyLeft <= 0f;

            /// <summary>The point picked at setup; the body moves there when it shows.</summary>
            public Vector3 Position { get; private set; }

            internal void Reset(float supply, Ship hero, Vector3 position)
            {
                Position = position;
                SupplyLeft = supply;
                HasArrived = false;
                Inside = false;
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

            internal void Draw(float amount)
            {
                SupplyLeft -= amount;
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
        protected void ConfigurePlacement(float distance, SectorHunt hunt, float spacing, params FindStation[] spacedFrom)
        {
            this.hunt = hunt;
            this.spacing = spacing;
            this.spacedFrom = spacedFrom;
            distanceMin = distanceMax = distance;
        }
#endif
    }
}
