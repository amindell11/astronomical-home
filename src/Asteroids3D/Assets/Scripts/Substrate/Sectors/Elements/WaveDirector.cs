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
using Substrate.Sessions;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>
    /// Sends waves of authored groups inside its sector phases: a phase opens with a wave, and the next
    /// comes at its ends at after the breather, or at the longest wait. The cap is a ceiling a wave waits
    /// under, counting the hunt's live patrols; nothing arrives in a lull, the done table keeps the last
    /// phase's waves, and the elite arriving stops them. The opening lull is held for the hunt. Ships of
    /// a wave arrive together just off screen, each facing the hero on its faction kit and skin. Dead
    /// products despawn on the tick, and the tick idles while the hero is inactive. Nothing spawns in
    /// Build, since the asteroid field lays out at its Start. The base class's "produce exactly once"
    /// is about the activation token, not lifetime.
    /// </summary>
    public class WaveDirector : SectorSpawner, ISectorRowReader
    {
        [Serializable]
        public struct RosterEntry
        {
            [Stat] public Ship template;
            [Stat] public Commander pilot;
        }

        [Tooltip("The sector's faction: every roster ship must be its signature or common ship, and flies that ship's faction kit. " +
                 "Unset → products fly their template's authored build.")]
        [SerializeField] private SectorFaction faction;
        [SerializeField] private int team = 1;

        [Header("Sector phases")]
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
        [Tooltip("Distance along the ring between ships of one wave.")]
        [SerializeField, Min(0f)] private float groupSpread = 8f;
        [Tooltip("A candidate point overlapping an asteroid within this radius is rejected.")]
        [SerializeField] private float clearanceRadius = 6f;
        [SerializeField] private LayerMask asteroidMask = 1 << 8;
        [Tooltip("Random angles tried per spawn; none clear skips this tick.")]
        [SerializeField, Min(1)] private int placementAttempts = 4;

        private readonly List<Ship> products = new();
        private SectorPhase[] phases = Array.Empty<SectorPhase>();
        private IUnitService units;
        private IObstacleField field;
        private Ship hero;
        private SectorPhaseSchedule schedule;
        private float startTime;
        private bool started;
        private bool huntReleased;
        private bool eliteArrived;
        private int openedPhase;
        private RosterEntry[] pending;
        private float lastWave;
        private float settledSince = float.PositiveInfinity;

        /// <summary>Where the phase table stands as of the last tick.</summary>
        public SectorPhaseMark Phase { get; private set; }

        public int PhaseCount => phases.Length;

        /// <summary>A group ship the row's faction doesn't fly fails at session start, not the first wave.</summary>
        public void CheckRow(SectorEntry row)
        {
            if (row.phases == null || row.phases.Length == 0)
                throw new InvalidOperationException(
                    $"WaveDirector on '{name}' reads the row's phases, and the row has none.");
            for (var i = 0; i < row.phases.Length; i++)
            {
                var phase = row.phases[i];
                if (phase.cap > 0 && (phase.groups == null || phase.groups.Length == 0))
                    throw new InvalidOperationException(
                        $"WaveDirector on '{name}' phase {i} has a cap and lists no groups.");
                if (phase.groups == null) continue;
                foreach (var group in phase.groups)
                {
                    if (group.ships == null || group.ships.Length == 0)
                        throw new InvalidOperationException(
                            $"WaveDirector on '{name}' phase {i} has a group with no ships.");
                    if (group.ships.Length > phase.cap)
                        throw new InvalidOperationException(
                            $"WaveDirector on '{name}' phase {i} has a group of {group.ships.Length} ships, past its cap of {phase.cap}.");
                    foreach (var ship in group.ships)
                        if (!ship.template)
                            throw new InvalidOperationException(
                                $"WaveDirector on '{name}' phase {i} has a group ship with no template.");
                }
            }

            if (!faction) return;
            if (!row.faction)
                throw new InvalidOperationException($"WaveDirector on '{name}' flies the row's faction, and the row names none.");
            foreach (var phase in row.phases)
            {
                if (phase.groups == null) continue;
                foreach (var group in phase.groups)
                    foreach (var ship in group.ships)
                        row.faction.KitFor(ship.template);
            }
        }

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            CheckRow(ctx.Row);
            products.Clear();
            Spawned = products;
            phases = ctx.Row.phases;

            if (!ctx.Hero)
            {
                Debug.LogError($"WaveDirector on '{name}' has no hero — director is inert.", this);
                yield break;
            }

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
            var wave = WaveDue(now, Alive);
            if (wave != null && TrySend(wave)) Arrived(now);
        }

        internal int Alive => products.Count + (hunt ? hunt.LivePatrols : 0);

        internal void OnEliteArrived(EliteMoment moment) => eliteArrived = true;

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
            pending = null;
            lastWave = now;
            settledSince = float.PositiveInfinity;
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

        internal RosterEntry[] WaveDue(float now, int alive)
        {
            if (Phase.InLull || eliteArrived) return null;
            var phase = phases[Phase.Index];
            if (OpensPhase())
                pending = Draw(phase);
            else if (pending == null && Settled(now, alive, phase))
                pending = Draw(phase);
            return pending != null && alive + pending.Length <= phase.cap ? pending : null;
        }

        private bool Settled(float now, int alive, SectorPhase phase)
        {
            if (alive > phase.endsAt)
                settledSince = float.PositiveInfinity;
            else
                settledSince = Mathf.Min(settledSince, now);
            return now >= settledSince + phase.breather || now >= lastWave + phase.longestWait;
        }

        internal void Arrived(float now)
        {
            pending = null;
            lastWave = now;
            settledSince = float.PositiveInfinity;
        }

        private static RosterEntry[] Draw(SectorPhase phase) =>
            phase.groups == null || phase.groups.Length == 0
                ? null
                : phase.groups[UnityEngine.Random.Range(0, phase.groups.Length)].ships;

        /// <summary>Reads only serialized data and the row, so a prefab asset answers too.</summary>
        public SectorThreat Threat(SectorEntry row)
        {
            var ships = new List<Ship>();
            if (row.phases != null)
                foreach (var phase in row.phases)
                    if (phase.groups != null)
                        foreach (var group in phase.groups)
                            if (group.ships != null)
                                foreach (var entry in group.ships)
                                    AddDistinct(ships, entry.template);

            var weapons = new List<WeaponComponent>();
            foreach (var ship in ships)
            {
                if (faction)
                {
                    AddDistinct(weapons, row.faction.KitFor(ship));
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

        private bool TrySend(RosterEntry[] group)
        {
            var center = GamePlane.WorldPointToPlane(hero.transform.position);
            for (var attempt = 0; attempt < placementAttempts; attempt++)
            {
                var bearing = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                var slots = Slots(center, bearing, group.Length);
                if (Array.Exists(slots, slot =>
                        Physics.CheckSphere(GamePlane.PlanePointToWorld(slot), clearanceRadius, asteroidMask)))
                    continue;

                for (var i = 0; i < group.Length; i++)
                {
                    var entry = group[i];
                    var outward = (slots[i] - center).normalized;
                    var facingHero = GamePlane.PlanePose(GamePlane.Normal, GamePlane.PlaneDirToWorld(-outward));
                    var position = GamePlane.PlanePointToWorld(slots[i]);
                    var loadout = faction ? new ShipLoadout(entry.template, KitFor(entry.template), null) : null;
                    var ship = units.SpawnShip(entry.template, entry.pilot, team, position, facingHero, field, loadout);
                    if (faction) faction.Faction.Dress(entry.template, ship, elite: false);
                    products.Add(ship);
                }
                return true;
            }
            return false;
        }

        internal Vector2[] Slots(Vector2 center, float bearing, int count)
        {
            var slots = new Vector2[count];
            var step = groupSpread / spawnRadius;
            for (var i = 0; i < count; i++)
            {
                var angle = bearing + (i - (count - 1) / 2f) * step;
                slots[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;
            }
            return slots;
        }

#if UNITY_EDITOR
        internal void Configure(SectorPhase[] phases, float lullSeconds = 5f, float spawnRadius = 55f,
            SectorFaction faction = null, float openingLullSeconds = 0f, SectorHunt hunt = null,
            EliteEncounter elite = null, float groupSpread = 8f)
        {
            this.hunt = hunt;
            this.elite = elite;
            this.faction = faction;
            this.phases = phases;
            this.lullSeconds = lullSeconds;
            this.openingLullSeconds = openingLullSeconds;
            this.spawnRadius = spawnRadius;
            this.groupSpread = groupSpread;
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
