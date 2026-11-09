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
    /// A sector's mod stations: the authored body plus copies of it, as many as the feel asks for. At setup,
    /// after the sector's repair stations pick theirs, each picks its own point away from the sector's start,
    /// spaced from the other mod stations and from those repair stations; the asteroid field reads those points
    /// and leaves a clearing of the station's clear radius. On the first tick each shows as a find until it goes
    /// dark. The hero's first fly-in claims it: <see cref="Claims"/> counts up, which the host reads to open
    /// one lull pick per claim, and the station goes dark. The hunt's alarm darkens unclaimed stations.
    /// A sector with no hunt keeps them live.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class ModStation : SectorModule
    {
        [Tooltip("Station count.")]
        [Stat, SerializeField] private ModStationFeelProfile feel;
        [Tooltip("The model with its zone and lit parts; copied per station and hidden until placed.")]
        [SerializeField] private ModStationBody body;
        [Tooltip("The sector's opening hunt, read for its alarm, which darkens every unclaimed station; unset for none.")]
        [SerializeField] private SectorHunt hunt;
        [Tooltip("The sector's repair stations, read at setup for their points so no mod station lands beside one; unset for none.")]
        [SerializeField] private RepairStation repairs;
        [Tooltip("The find icon its minimap marker and edge pointer show.")]
        [SerializeField] private Sprite icon;

        [Header("Placement")]
        [Tooltip("Plane distances from the hero's start the stations are placed between.")]
        [SerializeField, Min(0f)] private float distanceMin = 120f;
        [SerializeField, Min(0f)] private float distanceMax = 180f;
        [Tooltip("Least plane distance from a mod station to another mod station or a repair station.")]
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

        public StationKind Kind => StationKind.Mod;

        public IReadOnlyList<Station> Stations => stations;

        public float ClearRadius => clearRadius;

        /// <summary>Stations claimed this sector; the host opens one lull pick per claim.</summary>
        public int Claims { get; private set; }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel || !body || !body.Zone || !body.Lit || !icon || ctx.Objectives == null || !ctx.Sector
                || !body.Zone.transform.IsChildOf(body.transform) || !body.Lit.transform.IsChildOf(body.transform))
                throw new InvalidOperationException(
                    $"ModStation on '{name}' needs its feel profile, icon, objective service, sector, body, and a zone and lit parts under the body.");

            hero = ctx.Hero;
            objectives = ctx.Objectives;
            sectorIndex = ctx.SectorIndex;
            shown = false;
            Claims = 0;
            ClearStations();
            body.gameObject.SetActive(false);
            var taken = new List<Vector3>();
            if (repairs)
                foreach (var repair in repairs.Stations)
                    taken.Add(repair.Position);
            for (var i = 0; i < feel.stationsPerSector; i++)
            {
                var station = i == 0 ? new Station(body) : Copy();
                var point = SpacedPoint.Around(ctx.Sector.StartPoint, distanceMin, distanceMax, placementAttempts,
                    taken, spacing);
                taken.Add(point);
                station.Reset(hero, point);
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
            Tick();
        }

        private void Tick()
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
                if (station.Zone.IsPlayerInZone) Claim(station);
            }
        }

        private void Show()
        {
            foreach (var station in stations)
                station.Show(objectives.MarkFind(station.Body.transform, icon));
            shown = true;
        }

        private void Claim(Station station)
        {
            station.GoDark();
            Claims++;
            Arrived?.Invoke(Kind, sectorIndex);
            FindClaimed?.Invoke(Kind);
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

        /// <summary>One placed station of the sector's set, claimed at most once.</summary>
        public sealed class Station
        {
            internal readonly GameObject Body;
            internal readonly ExtractionZone Zone;
            internal readonly GameObject Lit;
            private FindMarker find;

            internal Station(ModStationBody body)
            {
                Body = body.gameObject;
                Zone = body.Zone;
                Lit = body.Lit;
            }

            /// <summary>Claimed, or darkened by the alarm.</summary>
            public bool Spent { get; private set; }

            /// <summary>The point picked at setup; the body moves there when it shows.</summary>
            public Vector3 Position { get; private set; }

            internal void Reset(Ship hero, Vector3 position)
            {
                Position = position;
                Spent = false;
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

            internal void GoDark()
            {
                Spent = true;
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
        internal void Configure(ModStationFeelProfile feel, float distance, SectorHunt hunt = null,
            float spacing = 0f, RepairStation repairs = null)
        {
            this.feel = feel;
            this.hunt = hunt;
            this.spacing = spacing;
            this.repairs = repairs;
            distanceMin = distanceMax = distance;
        }
#endif
    }
}
