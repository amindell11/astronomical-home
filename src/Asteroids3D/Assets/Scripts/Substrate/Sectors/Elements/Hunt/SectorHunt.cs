using System;
using System.Collections;
using System.Collections.Generic;
using AI;
using AI.Scanning;
using Balance;
using Damage;
using Ships;
using Substrate.Services.Units;
using UnityEngine;
using Utils;

namespace Substrate.Sectors.Elements.Hunt
{
    public enum HuntAlarmReason { Patrol, SearchClock }

    /// <summary>
    /// A sector's opening hunt. Patrols spawn on clear points away from the hero and roam with their scouts
    /// unaware, so they acquire no enemy and hold fire. A patrol that sees the hero (inside the sight radius,
    /// no asteroid in between, not concealed) or is hit by it starts calling the hero in; the alarm sounds when
    /// a call-in runs out, or when the search clock does, and a patrol killed first cancels its call-in. At the
    /// alarm every live patrol's scout wakes, so it fights like a wave ship. The wave director holds its
    /// opening lull until <see cref="Alarmed"/>. The events are plain C#, for listeners outside the sector.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class SectorHunt : SectorSpawner
    {
        [Tooltip("Search clock, sight and call-in timing.")]
        [Stat, SerializeField] private HuntFeelProfile feel;
        [Tooltip("The ship every patrol flies.")]
        [Stat, SerializeField] private Ship patrolShip;
        [Stat, SerializeField] private AICommander patrolPilot;
        [Stat, SerializeField, Min(0)] private int patrolCount = 1;
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
        private int sectorIndex;
        private bool started;
        private float startTime;

        /// <summary>A patrol saw the hero, or took its hit, and starts calling: sector index, patrol.</summary>
        public event Action<int, Ship> PatrolSpotted;

        /// <summary>The hunt ends: sector index, reason, calling patrol (null when the search clock ran out).</summary>
        public event Action<int, HuntAlarmReason, Ship> AlarmRaised;

        /// <summary>A patrol died before the alarm, cancelling any call-in: sector index, patrol.</summary>
        public event Action<int, Ship> PatrolSilenced;

        public bool Alarmed { get; private set; }

        public float SearchSecondsLeft { get; private set; }

        /// <summary>The soonest call-in's seconds to the alarm; null while no patrol is calling.</summary>
        public float? CallInSecondsLeft { get; private set; }

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            products.Clear();
            patrols.Clear();
            Spawned = products;
            if (!feel || patrolCount > 0 && (!patrolShip || !patrolPilot))
                throw new InvalidOperationException(
                    $"SectorHunt on '{name}' needs its feel profile, and a patrol ship and pilot when it spawns patrols.");

            units = ctx.Units;
            field = ctx.Field;
            hero = ctx.Hero;
            sectorIndex = ctx.SectorIndex;
            started = false;
            Alarmed = false;
            SearchSecondsLeft = feel.searchSeconds;
            CallInSecondsLeft = null;
            yield break;
        }

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            foreach (var patrol in patrols) patrol.Ship.Damage.OnDamaged -= patrol.OnDamaged;
            patrols.Clear();
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
            PatrolSpotted?.Invoke(sectorIndex, patrol.Ship);
        }

        private void Sound(HuntAlarmReason reason, Ship caller)
        {
            Alarmed = true;
            CallInSecondsLeft = null;
            foreach (var patrol in patrols) patrol.Scout.Unaware = false;
            AlarmRaised?.Invoke(sectorIndex, reason, caller);
        }

        private void SpawnPatrols()
        {
            var center = GamePlane.WorldPointToPlane(hero.transform.position);
            for (var i = 0; i < patrolCount; i++)
            {
                var angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                var heading = GamePlane.PlaneDirToWorld(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                var ship = units.SpawnShip(patrolShip, patrolPilot, team, ClearPointAround(center),
                    GamePlane.PlanePose(GamePlane.Normal, heading), field);
                var patrol = new Patrol(ship, ((AICommander)ship.Commander).Scout);
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

        private Vector3 ClearPointAround(Vector2 center)
        {
            var position = Vector3.zero;
            for (var attempt = 0; attempt < placementAttempts; attempt++)
            {
                var angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                var distance = UnityEngine.Random.Range(spawnDistanceMin, spawnDistanceMax);
                position = GamePlane.PlanePointToWorld(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
                if (!Physics.CheckSphere(position, clearanceRadius, asteroidMask)) break;
            }
            return position;
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
                if (!Alarmed) PatrolSilenced?.Invoke(sectorIndex, ship);
                units.DespawnShip(ship);
            }
        }

        private sealed class Patrol
        {
            public readonly Ship Ship;
            public readonly Scout Scout;
            public Action<AppliedDamage> OnDamaged;
            public float CallInEndsAt = float.PositiveInfinity;

            public Patrol(Ship ship, Scout scout)
            {
                Ship = ship;
                Scout = scout;
            }

            public bool CallingIn => !float.IsPositiveInfinity(CallInEndsAt);
        }

#if UNITY_EDITOR
        internal void Configure(HuntFeelProfile feel, Ship patrolShip, AICommander patrolPilot, int patrolCount,
            float spawnDistance)
        {
            this.feel = feel;
            this.patrolShip = patrolShip;
            this.patrolPilot = patrolPilot;
            this.patrolCount = patrolCount;
            spawnDistanceMin = spawnDistanceMax = spawnDistance;
        }
#endif
    }
}
