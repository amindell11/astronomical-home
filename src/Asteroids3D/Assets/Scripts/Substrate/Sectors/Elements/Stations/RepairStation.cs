using System;
using System.Collections;
using Balance;
using Objectives;
using Ships;
using Substrate.Sectors.Elements.Placement;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's repair station. On its first tick it moves to a clear point away from the hero's start and
    /// shows. While the hero sits in its zone it restores hull at the feel's rate from a supply that drains only
    /// by what it heals, and it goes dark (glow and minimap marker off) when the supply is spent. It works from
    /// the sector's start to its end, through the hunt and after the alarm. The arrival events are plain C#,
    /// raised once on the first arrival, for listeners outside the sector.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class RepairStation : SectorModule
    {
        [Tooltip("Repair rate and supply.")]
        [Stat, SerializeField] private RepairStationFeelProfile feel;
        [Tooltip("The fly-in trigger, on the body.")]
        [SerializeField] private ExtractionZone zone;
        [Tooltip("The model, zone and lit parts; hidden until the station is placed.")]
        [SerializeField] private GameObject body;
        [Tooltip("The glow and the minimap marker; hidden when the supply is spent.")]
        [SerializeField] private GameObject lit;

        [Header("Placement")]
        [Tooltip("Plane distances from the hero's start the station is placed between.")]
        [SerializeField, Min(0f)] private float distanceMin = 120f;
        [SerializeField, Min(0f)] private float distanceMax = 180f;
        [Tooltip("A candidate point with an asteroid within this radius is passed over.")]
        [SerializeField, Min(0f)] private float clearanceRadius = 35f;
        [SerializeField] private LayerMask asteroidMask = 1 << 8;
        [Tooltip("Random points tried; the last one tried is used when none is clear.")]
        [SerializeField, Min(1)] private int placementAttempts = 8;

        private Ship hero;
        private int sectorIndex;
        private bool placed;
        private bool arrived;

        /// <summary>The hero reached the station for the first time: station kind, sector index.</summary>
        public event Action<StationKind, int> Arrived;

        /// <summary>The hero claimed this find, on its first arrival: station kind.</summary>
        public event Action<StationKind> FindClaimed;

        public StationKind Kind => StationKind.Repair;

        /// <summary>Hull left to give, as a fraction of a full hull.</summary>
        public float SupplyLeft { get; private set; }

        public bool Spent => SupplyLeft <= 0f;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel || !zone || !body || !lit)
                throw new InvalidOperationException(
                    $"RepairStation on '{name}' needs its feel profile, zone, body and lit parts wired.");

            hero = ctx.Hero;
            sectorIndex = ctx.SectorIndex;
            placed = false;
            arrived = false;
            SupplyLeft = feel.supply;
            zone.BindPlayer(hero ? hero.Body : null);
            zone.Disarm();
            body.SetActive(false);
            lit.SetActive(true);
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            zone.Disarm();
            body.SetActive(false);
            hero = null;
            yield break;
        }

        private void Update()
        {
            if (!hero || !hero.gameObject.activeInHierarchy) return;
            Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            if (!placed)
            {
                Place();
                return;
            }
            if (!zone.IsPlayerInZone) return;

            if (!arrived)
            {
                arrived = true;
                Arrived?.Invoke(Kind, sectorIndex);
                FindClaimed?.Invoke(Kind);
            }
            Repair(deltaTime);
        }

        private void Place()
        {
            var start = GamePlane.WorldPointToPlane(hero.transform.position);
            transform.position = ClearPoint.Around(start, distanceMin, distanceMax, clearanceRadius, asteroidMask,
                placementAttempts);
            body.SetActive(true);
            zone.Arm();
            placed = true;
        }

        private void Repair(float deltaTime)
        {
            var hull = hero.Damage.Health;
            var heal = Mathf.Min(feel.repairPerSecond * deltaTime, SupplyLeft, 1f - hull.Pct);
            if (heal <= 0f) return;

            hull.RestoreFraction(heal);
            SupplyLeft -= heal;
            if (Spent) GoDark();
        }

        private void GoDark()
        {
            SupplyLeft = 0f;
            zone.Disarm();
            lit.SetActive(false);
        }

#if UNITY_EDITOR
        internal void Configure(RepairStationFeelProfile feel, float distance)
        {
            this.feel = feel;
            distanceMin = distanceMax = distance;
        }
#endif
    }
}
