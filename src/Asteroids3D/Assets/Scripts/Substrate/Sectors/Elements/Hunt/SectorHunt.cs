using System;
using System.Collections;
using System.Collections.Generic;
using AI;
using AI.Scanning;
using Balance;
using Damage;
using Ships;
using Ships.Loadout;
using Ships.Presentation;
using Substrate.Sectors.Elements.Placement;
using Substrate.Sectors.Factions;
using Substrate.Services.Units;
using UnityEngine;
using Utils;

namespace Substrate.Sectors.Elements.Hunt
{
    public enum HuntAlarmReason { Patrol, SearchClock }

    /// <summary>
    /// A sector's opening hunt. Patrols spawn on clear points away from the hero and fly a looped route past
    /// the hero's start, through the sector center and back, their scouts unaware, so they acquire no enemy and
    /// hold fire. A patrol that sees the hero (inside the sight radius, no asteroid in between, not concealed)
    /// or is hit by it starts calling the hero in, its minimap marker pulsing; the alarm sounds when a call-in
    /// runs out, or when the search clock does, and a patrol killed first cancels its call-in. At the alarm
    /// every live patrol leaves its route and its scout wakes, so it fights like a wave ship. The wave director
    /// holds its opening lull until <see cref="Alarmed"/> and counts <see cref="LivePatrols"/> against its cap.
    /// The events are plain C#, for listeners outside the sector.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class SectorHunt : SectorSpawner
    {
        [Tooltip("Search clock, sight and call-in timing.")]
        [Stat, SerializeField] private HuntFeelProfile feel;
        [Tooltip("The sector's faction: every patrol flies its common ship, on its kit and in its skin.")]
        [Stat, SerializeField] private SectorFaction faction;
        [Stat, SerializeField] private AICommander patrolPilot;
        [SerializeField] private int team = 1;

        [Header("Placement")]
        [Tooltip("Plane distances from the hero's start that patrols spawn between; author them past the sight radius.")]
        [SerializeField, Min(0f)] private float spawnDistanceMin = 100f;
        [SerializeField, Min(0f)] private float spawnDistanceMax = 150f;
        [Tooltip("A candidate point with an asteroid within this radius is passed over.")]
        [SerializeField, Min(0f)] private float clearanceRadius = 10f;
        [Tooltip("Asteroids: spawn clearance and cover from a patrol's sight.")]
        [SerializeField] private LayerMask asteroidMask = 1 << 8;
        [Tooltip("Random points tried per patrol; the last one tried is used when none is clear.")]
        [SerializeField, Min(1)] private int placementAttempts = 8;

        private readonly List<Ship> products = new();
        private readonly List<Patrol> patrols = new();
        private IUnitService units;
        private IObstacleField field;
        private Ship hero;
        private Sector sector;
        private int sectorIndex;
        private int patrolCount;
        private bool started;
        private float startTime;

        /// <summary>A patrol saw the hero, or took its hit, and starts calling: sector index, patrol.</summary>
        public event Action<int, Ship> PatrolSpotted;

        /// <summary>The hunt ends: sector index, reason, calling patrol (null when the search clock ran out).</summary>
        public event Action<int, HuntAlarmReason, Ship> AlarmRaised;

        /// <summary>A patrol died before the alarm, cancelling any call-in: sector index, patrol.</summary>
        public event Action<int, Ship> PatrolSilenced;

        public bool Alarmed { get; private set; }

        public int LivePatrols => patrols.Count;

        public float SearchSecondsLeft { get; private set; }

        /// <summary>The soonest call-in's seconds to the alarm; null while no patrol is calling.</summary>
        public float? CallInSecondsLeft { get; private set; }

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            products.Clear();
            patrols.Clear();
            Spawned = products;
            if (!feel)
                throw new InvalidOperationException($"SectorHunt on '{name}' needs its feel profile.");
            if (ctx.SectorIndex < 0 || ctx.SectorIndex >= feel.patrolsPerSector.Length)
                throw new InvalidOperationException(
                    $"'{feel.name}' lists patrols for {feel.patrolsPerSector.Length} sectors, not for sector {ctx.SectorIndex}.");
            patrolCount = feel.patrolsPerSector[ctx.SectorIndex];
            if (patrolCount > 0 && (!faction || !patrolPilot || !ctx.Sector))
                throw new InvalidOperationException(
                    $"SectorHunt on '{name}' needs a faction, patrol pilot and sector when it spawns patrols.");

            units = ctx.Units;
            field = ctx.Field;
            hero = ctx.Hero;
            sector = ctx.Sector;
            sectorIndex = ctx.SectorIndex;
            started = false;
            Alarmed = false;
            SearchSecondsLeft = feel.searchSeconds;
            CallInSecondsLeft = null;
            yield break;
        }

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            foreach (var patrol in patrols)
            {
                patrol.Ship.Damage.OnDamaged -= patrol.OnDamaged;
                patrol.Release();
            }
            patrols.Clear();
            hero = null;
            sector = null;
            var teardown = base.OnTeardown(ctx);
            while (teardown.MoveNext()) yield return teardown.Current;
            products.Clear();
            units = null;
            field = null;
        }

        private void Update()
        {
            if (!hero || !hero.gameObject.activeInHierarchy) return;
            Tick(Time.time);
        }

        internal void Tick(float now)
        {
            if (!started)
            {
                started = true;
                startTime = now;
                SpawnPatrols();
            }

            DespawnDead();
            if (Alarmed) return;

            foreach (var patrol in patrols) patrol.FlyRoute(feel.routeSpeed, feel.arrivalRadius);
            foreach (var patrol in patrols)
                if (!patrol.CallingIn && Sees(patrol.Ship)) BeginCallIn(patrol, now);

            CallInSecondsLeft = null;
            foreach (var patrol in patrols)
            {
                if (!patrol.CallingIn) continue;
                if (now >= patrol.CallInEndsAt)
                {
                    Sound(HuntAlarmReason.Patrol, patrol.Ship);
                    return;
                }
                CallInSecondsLeft = Mathf.Min(CallInSecondsLeft ?? float.MaxValue, patrol.CallInEndsAt - now);
            }

            SearchSecondsLeft = Mathf.Max(0f, feel.searchSeconds - (now - startTime));
            if (SearchSecondsLeft <= 0f) Sound(HuntAlarmReason.SearchClock, null);
        }

        private bool Sees(Ship patrol)
        {
            if (hero.Lock.IsConcealed) return false;
            var from = patrol.transform.position;
            var to = hero.transform.position;
            return (to - from).sqrMagnitude <= feel.sightRadius * feel.sightRadius
                && LineOfSight.IsClear(from, to, hero.transform, asteroidMask);
        }

        private void BeginCallIn(Patrol patrol, float now)
        {
            patrol.CallInEndsAt = now + feel.callInSeconds;
            if (patrol.Rig) patrol.Rig.PulseMinimapMarker(true);
            PatrolSpotted?.Invoke(sectorIndex, patrol.Ship);
        }

        private void Sound(HuntAlarmReason reason, Ship caller)
        {
            Alarmed = true;
            CallInSecondsLeft = null;
            foreach (var patrol in patrols)
            {
                patrol.Release();
                patrol.Scout.Unaware = false;
            }
            AlarmRaised?.Invoke(sectorIndex, reason, caller);
        }

        private void SpawnPatrols()
        {
            var center = GamePlane.WorldPointToPlane(hero.transform.position);
            for (var i = 0; i < patrolCount; i++)
            {
                var angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                var heading = GamePlane.PlaneDirToWorld(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                var patrolShip = faction.Faction.common.ship;
                var position = ClearPoint.Around(center, spawnDistanceMin, spawnDistanceMax, clearanceRadius,
                    asteroidMask, placementAttempts);
                var ship = units.SpawnShip(patrolShip, patrolPilot, team, position,
                    GamePlane.PlanePose(GamePlane.Normal, heading), field,
                    new ShipLoadout(patrolShip, faction.Faction.KitFor(patrolShip), null));
                faction.Faction.Dress(patrolShip, ship, elite: false);
                var route = PatrolRoute.Waypoints(center, GamePlane.WorldPointToPlane(sector.transform.position),
                    GamePlane.WorldPointToPlane(position), feel.routeStartPass);
                var patrol = new Patrol(ship, (AICommander)ship.Commander, route);
                patrol.Scout.Unaware = true;
                patrol.OnDamaged = hit =>
                {
                    if (!Alarmed && !patrol.CallingIn && hero && hit.Info.AttackerId == hero.Id)
                        BeginCallIn(patrol, Time.time);
                };
                ship.Damage.OnDamaged += patrol.OnDamaged;
                patrols.Add(patrol);
                products.Add(ship);
            }
        }

        private void DespawnDead()
        {
            for (var i = patrols.Count - 1; i >= 0; i--)
            {
                var patrol = patrols[i];
                var ship = patrol.Ship;
                if (ship && ship.gameObject.activeInHierarchy) continue;
                patrols.RemoveAt(i);
                products.Remove(ship);
                if (!ship) continue;
                ship.Damage.OnDamaged -= patrol.OnDamaged;
                patrol.Release();
                if (!Alarmed) PatrolSilenced?.Invoke(sectorIndex, ship);
                units.DespawnShip(ship);
            }
        }

        private sealed class Patrol
        {
            public readonly Ship Ship;
            public readonly AICommander Commander;
            public readonly Scout Scout;
            // A presentation-less ship has no rig, or a dark one: nothing pulses.
            public readonly ShipVisualRig Rig;
            private readonly Vector2[] route;
            private int leg;
            public Action<AppliedDamage> OnDamaged;
            public float CallInEndsAt = float.PositiveInfinity;

            public Patrol(Ship ship, AICommander commander, Vector2[] route)
            {
                Ship = ship;
                Commander = commander;
                Scout = commander.Scout;
                Rig = ship.VisualRig;
                this.route = route;
            }

            public bool CallingIn => !float.IsPositiveInfinity(CallInEndsAt);

            public void FlyRoute(float speed, float arrivalRadius)
            {
                var here = GamePlane.WorldPointToPlane(Ship.transform.position);
                if ((route[leg] - here).sqrMagnitude <= arrivalRadius * arrivalRadius) leg = (leg + 1) % route.Length;
                Commander.SteerTo(route[leg], speed);
            }

            public void Release()
            {
                Commander.ClearSteer();
                if (Rig) Rig.PulseMinimapMarker(false);
            }
        }

#if UNITY_EDITOR
        internal void Configure(HuntFeelProfile feel, SectorFaction faction, AICommander patrolPilot,
            float spawnDistance)
        {
            this.feel = feel;
            this.faction = faction;
            this.patrolPilot = patrolPilot;
            spawnDistanceMin = spawnDistanceMax = spawnDistance;
        }
#endif
    }
}
