using Balance;
using System;
using System.Collections;
using System.Collections.Generic;
using AI.Scanning;
using Combat.Weapons;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Services.Units;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>
    /// Continuous producer for the survival trial, paced by a table of sector phases: each phase
    /// opens by filling to its alive cap, then spawns one ship per interval while under it. The opening
    /// lull before the first phase (held until the alarm when the sector has a hunt, so phase one keeps
    /// index 0) and the lull before each later one spawn nothing, and once the table is done the last
    /// phase's pacing holds, so the hero leaves under pursuit; the sector's elite arriving stops spawning
    /// for good. Ships ring the hero just off screen, facing it, on the first angle clear of asteroids,
    /// each flying its sector faction's kit for its ship and wearing the faction's skin.
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
        [Tooltip("The sector's faction: every roster ship must be its signature or common ship, and flies that ship's faction kit. " +
                 "Unset → products fly their template's authored build.")]
        [Stat, SerializeField] private SectorFaction faction;
        [SerializeField] private int team = 1;

        [Header("Sector phases")]
        [Tooltip("Phases in run order; the clock starts on the first tick with an active hero.")]
        [Stat(Ordered = true), SerializeField] private SectorPhase[] phases = Array.Empty<SectorPhase>();
        [Tooltip("Seconds between one phase's end and the next phase's opening, with nothing spawning.")]
        [Stat, SerializeField, Min(0f)] private float lullSeconds = 5f;
        [Tooltip("Seconds from the sector's start to phase one's opening, with nothing spawning.")]
        [Stat, SerializeField, Min(0f)] private float openingLullSeconds = 5f;
        [Tooltip("The sector's opening hunt. Set, the opening lull lasts until its alarm; unset, the seconds above.")]
        [SerializeField] private SectorHunt hunt;
        [Tooltip("The elite the sector's spine summons. Set, spawning stops when it arrives; it must be the spine's own elite.")]
        [SerializeField] private EliteEncounter elite;

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
        private bool huntReleased;
        private bool eliteArrived;
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

            // A roster ship the faction doesn't fly fails the load, not the first spawn.
            foreach (var entry in roster)
                KitFor(entry.template);
            BuildSchedule();

            units = ctx.Units;
            field = ctx.Field;
            hero = ctx.Hero;
            started = false;
            eliteArrived = false;
            if (elite) elite.Announced += OnEliteArrived;
            Phase = schedule.At(0f);
        }

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            if (elite) elite.Announced -= OnEliteArrived;
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
            HoldForHunt(now);
            Advance(now);
            if (Phase.InLull || eliteArrived) return;

            var row = phases[Phase.Index];
            if (OpensPhase())
            {
                for (var i = products.Count; i < row.cap; i++)
                    TrySpawn();
                ScheduleNext(now);
                return;
            }

            if (SpawnDue(now, products.Count) && TrySpawn())
                ScheduleNext(now);
        }

        private void OnEliteArrived(EliteMoment moment) => eliteArrived = true;

        private void OnValidate()
        {
            var spine = GetComponentInParent<PhaseSpineModule>(true);
            if (spine && spine.Elite is EliteEncounter owned && owned != elite)
                throw new InvalidOperationException(
                    $"WaveDirector on '{name}' must watch its spine's elite '{owned.name}', so the waves stop when it arrives.");
        }

        private void BuildSchedule() => schedule = new SectorPhaseSchedule(phases, lullSeconds, openingLullSeconds);

        internal void BeginSchedule(float now)
        {
            startTime = now;
            openedPhase = -1;
            huntReleased = false;
            Phase = schedule.At(0f);
        }

        /// <summary>Holds the opening lull at its start until the hunt's alarm, then ends it, once.</summary>
        internal void HoldForHunt(float now)
        {
            if (!hunt || huntReleased || !Phase.InOpeningLull) return;
            huntReleased = hunt.Alarmed;
            startTime = huntReleased ? now - openingLullSeconds : now;
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

        /// <summary>Reads only serialized data, so a prefab asset answers too.</summary>
        public SectorThreat Threat()
        {
            var ships = new List<Ship>();
            foreach (var entry in roster)
                AddDistinct(ships, entry.template);

            var weapons = new List<WeaponComponent>();
            foreach (var ship in ships)
            {
                if (faction)
                {
                    AddDistinct(weapons, KitFor(ship));
                    continue;
                }
                AddDistinct(weapons, ship.Weapons ? ship.Weapons.PrimaryMountPrefab : null);
                AddDistinct(weapons, ship.Weapons ? ship.Weapons.SecondaryMountPrefab : null);
            }
            return new SectorThreat(ships, weapons);
        }

        private static void AddDistinct<T>(List<T> list, T item) where T : UnityEngine.Object
        {
            if (item && !list.Contains(item)) list.Add(item);
        }

        private WeaponComponent KitFor(Ship chassis) => faction ? faction.Faction.KitFor(chassis) : null;

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
                var loadout = faction ? new ShipLoadout(entry.template, KitFor(entry.template), null) : null;
                var ship = units.SpawnShip(entry.template, entry.pilot, team, position, facingHero, field, loadout);
                if (faction) faction.Faction.Dress(entry.template, ship, elite: false);
                products.Add(ship);
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        internal void Configure(RosterEntry[] roster, SectorPhase[] phases, float lullSeconds = 5f,
            float spawnRadius = 55f, SectorFaction faction = null, float openingLullSeconds = 0f, SectorHunt hunt = null,
            EliteEncounter elite = null)
        {
            this.hunt = hunt;
            this.elite = elite;
            this.roster = roster;
            this.faction = faction;
            this.phases = phases;
            this.lullSeconds = lullSeconds;
            this.openingLullSeconds = openingLullSeconds;
            this.spawnRadius = spawnRadius;
            BuildSchedule();
        }
#endif
    }

    /// <summary>What a sector can send: its ships and the weapons they can carry, each listed once.</summary>
    public readonly struct SectorThreat
    {
        public IReadOnlyList<Ship> Ships { get; }
        public IReadOnlyList<WeaponComponent> Weapons { get; }

        public SectorThreat(IReadOnlyList<Ship> ships, IReadOnlyList<WeaponComponent> weapons)
        {
            Ships = ships;
            Weapons = weapons;
        }
    }
}
