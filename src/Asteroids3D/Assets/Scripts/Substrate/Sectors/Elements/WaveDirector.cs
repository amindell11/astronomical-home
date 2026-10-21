using Balance;
using System;
using System.Collections;
using System.Collections.Generic;
using AI.Scanning;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services.Units;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>
    /// Continuous producer for the survival trial, paced by a table of sector phases: each phase
    /// opens by filling to its alive cap, then spawns one ship per interval while under it. The lull
    /// before each later phase spawns nothing, and once the table is done the last phase's pacing
    /// holds, so the hero leaves under pursuit. Ships ring the hero just off screen, facing it, on the
    /// first angle clear of asteroids, each spawned with a build drawn from its phase's loadout pool.
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
            [Stat] public Ship template;
            [Stat] public Commander pilot;
        }

        [Tooltip("Ships the director draws from, picked uniformly per spawn.")]
        [Stat, SerializeField] private RosterEntry[] roster = Array.Empty<RosterEntry>();
        [Tooltip("Engine, shield and weapon pool each spawn draws its build from; the chassis stays the roster's. " +
                 "A phase may name its own pool. Unset → products fly their template's authored build.")]
        [Stat, SerializeField] private ItemSubset loadouts;
        [SerializeField] private int team = 1;

        [Header("Sector phases")]
        [Tooltip("Phases in run order; the clock starts on the first tick with an active hero.")]
        [Stat(Ordered = true), SerializeField] private SectorPhase[] phases = Array.Empty<SectorPhase>();
        [Tooltip("Seconds between one phase's end and the next phase's opening, with nothing spawning.")]
        [Stat, SerializeField, Min(0f)] private float lullSeconds = 5f;

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
        private SectorPhaseSchedule schedule;
        private float startTime;
        private float nextSpawnTime;
        private bool started;
        private int openedPhase;

        /// <summary>Where the phase table stands as of the last tick.</summary>
        public SectorPhaseMark Phase { get; private set; }

        public int PhaseCount => phases.Length;

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            products.Clear();
            Spawned = products;

            if (!ctx.Hero || roster.Length == 0 || Array.Exists(roster, e => !e.template))
            {
                Debug.LogError($"WaveDirector on '{name}' has no hero or an empty/unset roster — director is inert.", this);
                yield break;
            }

            RequireComplete(loadouts);
            foreach (var phase in phases)
                RequireComplete(phase.loadouts);
            BuildSchedule();

            units = ctx.Units;
            field = ctx.Field;
            hero = ctx.Hero;
            started = false;
            Phase = schedule.At(0f);
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
            if (!started)
            {
                started = true;
                BeginSchedule(now);
            }

            DespawnDead();
            Advance(now);
            if (Phase.InLull) return;

            var row = phases[Phase.Index];
            if (OpensPhase())
            {
                for (var i = products.Count; i < row.cap; i++)
                    TrySpawn(row);
                ScheduleNext(now);
                return;
            }

            if (SpawnDue(now, products.Count) && TrySpawn(row))
                ScheduleNext(now);
        }

        private void BuildSchedule() => schedule = new SectorPhaseSchedule(phases, lullSeconds);

        internal void BeginSchedule(float now)
        {
            startTime = now;
            openedPhase = -1;
            Phase = schedule.At(0f);
        }

        internal void Advance(float now) => Phase = schedule.At(now - startTime);

        /// <summary>True once, on the first tick of each phase; a lull or the done table opens nothing.</summary>
        internal bool OpensPhase()
        {
            if (Phase.Done || Phase.InLull || Phase.Index == openedPhase) return false;
            openedPhase = Phase.Index;
            return true;
        }

        internal bool SpawnDue(float now, int alive) =>
            !Phase.InLull && now >= nextSpawnTime && alive < phases[Phase.Index].cap;

        internal void ScheduleNext(float now) => nextSpawnTime = now + phases[Phase.Index].interval;

        internal ItemSubset PoolFor(SectorPhase row) => row.loadouts ? row.loadouts : loadouts;

        /// <summary>One uniform pick per slot; no secondary, since AI aim leads for the primary only.</summary>
        internal static ShipLoadout Draw(Ship chassis, ItemSubset pool) =>
            new(chassis, Pick(pool.engines), Pick(pool.shields), Pick(pool.weapons), null);

        private static T Pick<T>(T[] pool) => pool[UnityEngine.Random.Range(0, pool.Length)];

        private void RequireComplete(ItemSubset pool)
        {
            if (pool && (IsEmpty(pool.engines) || IsEmpty(pool.shields) || IsEmpty(pool.weapons)))
                throw new InvalidOperationException(
                    $"WaveDirector on '{name}': loadout pool '{pool.name}' needs at least one engine, shield and weapon.");
        }

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

        private bool TrySpawn(SectorPhase row)
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
                var pool = PoolFor(row);
                products.Add(units.SpawnShip(entry.template, entry.pilot, team, position, facingHero, field,
                    pool ? Draw(entry.template, pool) : null));
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        internal void Configure(RosterEntry[] roster, SectorPhase[] phases, float lullSeconds = 5f,
            float spawnRadius = 55f, ItemSubset loadouts = null)
        {
            this.roster = roster;
            this.loadouts = loadouts;
            this.phases = phases;
            this.lullSeconds = lullSeconds;
            this.spawnRadius = spawnRadius;
            BuildSchedule();
        }
#endif
    }
}
