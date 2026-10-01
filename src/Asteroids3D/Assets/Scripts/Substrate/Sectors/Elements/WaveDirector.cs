using System;
using System.Collections;
using System.Collections.Generic;
using AI.Scanning;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Substrate.Services.Units;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>
    /// Continuous producer for the survival trial: an opening wave on the first tick, then one spawn
    /// per interval while fewer than the alive cap live, interval and cap easing linearly from their
    /// start to end values over the ramp. Ships ring the hero just off screen, facing it, on the
    /// first angle clear of asteroids, each spawned with a build drawn from the loadout pool.
    /// Dead products are despawned on the tick, and the tick idles while the hero is inactive (the
    /// recap hold). Nothing spawns in Build: the sector builds under an inactive holder and the
    /// asteroid field lays out at its Start, so a Build-time clearance check would see no rocks.
    /// The base class's "produce exactly once" is about the activation token, not lifetime.
    /// </summary>
    public class WaveDirector : SectorSpawner
    {
        [Serializable]
        public struct RosterEntry
        {
            public Ship template;
            public Commander pilot;
        }

        [Tooltip("Ships the director draws from, picked uniformly per spawn.")]
        [SerializeField] private RosterEntry[] roster = Array.Empty<RosterEntry>();
        [Tooltip("Engine, shield and weapon pool each spawn draws its build from; the chassis stays the roster's. " +
                 "Unset → products fly their template's authored build.")]
        [SerializeField] private ItemSubset loadouts;
        [SerializeField] private int team = 1;

        [Header("Escalation")]
        [SerializeField, Min(0.1f)] private float startInterval = 6f;
        [SerializeField, Min(0.1f)] private float endInterval = 2f;
        [SerializeField, Min(0)] private int startCap = 2;
        [SerializeField, Min(0)] private int endCap = 8;
        [Tooltip("Seconds from Build until interval and cap reach their end values.")]
        [SerializeField, Min(1f)] private float rampSeconds = 240f;

        [Header("Placement")]
        [Tooltip("Plane distance from the hero; author it past the camera's widest view so ships arrive off screen.")]
        [SerializeField] private float spawnRadius = 55f;
        [Tooltip("A candidate point overlapping an asteroid within this radius is rejected.")]
        [SerializeField] private float clearanceRadius = 6f;
        [SerializeField] private LayerMask asteroidMask = 1 << 8;
        [Tooltip("Random angles tried per spawn; none clear skips this tick.")]
        [SerializeField, Min(1)] private int placementAttempts = 4;

        private readonly List<Ship> products = new();
        private IUnitService units;
        private IObstacleField field;
        private Ship hero;
        private float startTime;
        private float nextSpawnTime;
        private bool opened;

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            products.Clear();
            Spawned = products;

            if (!ctx.Hero || roster.Length == 0 || Array.Exists(roster, e => !e.template))
            {
                Debug.LogError($"WaveDirector on '{name}' has no hero or an empty/unset roster — director is inert.", this);
                yield break;
            }

            if (loadouts && (IsEmpty(loadouts.engines) || IsEmpty(loadouts.shields) || IsEmpty(loadouts.weapons)))
                throw new InvalidOperationException(
                    $"WaveDirector on '{name}': loadout pool '{loadouts.name}' needs at least one engine, shield and weapon.");

            units = ctx.Units;
            field = ctx.Field;
            hero = ctx.Hero;
            opened = false;
        }

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            hero = null;
            var teardown = base.OnTeardown(ctx);
            while (teardown.MoveNext()) yield return teardown.Current;
            products.Clear();
            units = null;
            field = null;
        }

        private void Update()
        {
            if (!hero || !hero.gameObject.activeInHierarchy) return;

            var now = Time.time;
            if (!opened)
            {
                opened = true;
                BeginSchedule(now);
                for (var i = 0; i < CapAt(0f); i++)
                    TrySpawn();
                return;
            }

            DespawnDead();
            if (SpawnDue(now, products.Count) && TrySpawn())
                ScheduleNext(now);
        }

        internal void BeginSchedule(float now)
        {
            startTime = now;
            ScheduleNext(now);
        }

        internal bool SpawnDue(float now, int alive) =>
            now >= nextSpawnTime && alive < CapAt(now - startTime);

        internal void ScheduleNext(float now) => nextSpawnTime = now + IntervalAt(now - startTime);

        internal float IntervalAt(float elapsed) => Mathf.Lerp(startInterval, endInterval, elapsed / rampSeconds);

        internal int CapAt(float elapsed) => Mathf.RoundToInt(Mathf.Lerp(startCap, endCap, elapsed / rampSeconds));

        /// <summary>One uniform pick per slot; no secondary, since AI aim leads for the primary only.</summary>
        internal ShipLoadout Draw(Ship chassis) =>
            new(chassis, Pick(loadouts.engines), Pick(loadouts.shields), Pick(loadouts.weapons), null);

        private static T Pick<T>(T[] pool) => pool[UnityEngine.Random.Range(0, pool.Length)];

        private static bool IsEmpty<T>(T[] pool) => pool == null || pool.Length == 0;

        private void DespawnDead()
        {
            for (var i = products.Count - 1; i >= 0; i--)
            {
                var ship = products[i];
                if (ship && ship.gameObject.activeInHierarchy) continue;
                if (ship) units.DespawnShip(ship);
                products.RemoveAt(i);
            }
        }

        private bool TrySpawn()
        {
            var center = GamePlane.WorldPointToPlane(hero.transform.position);
            for (var attempt = 0; attempt < placementAttempts; attempt++)
            {
                var angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var position = GamePlane.PlanePointToWorld(center + outward * spawnRadius);
                if (Physics.CheckSphere(position, clearanceRadius, asteroidMask)) continue;

                var entry = roster[UnityEngine.Random.Range(0, roster.Length)];
                var facingHero = GamePlane.PlanePose(GamePlane.Normal, GamePlane.PlaneDirToWorld(-outward));
                products.Add(units.SpawnShip(entry.template, entry.pilot, team, position, facingHero, field,
                    loadouts ? Draw(entry.template) : null));
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        internal void Configure(RosterEntry[] roster, float startInterval, float endInterval,
            int startCap, int endCap, float rampSeconds, float spawnRadius = 55f, ItemSubset loadouts = null)
        {
            this.roster = roster;
            this.loadouts = loadouts;
            this.startInterval = startInterval;
            this.endInterval = endInterval;
            this.startCap = startCap;
            this.endCap = endCap;
            this.rampSeconds = rampSeconds;
            this.spawnRadius = spawnRadius;
        }
#endif
    }
}
