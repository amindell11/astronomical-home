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
using Substrate.Sessions;
using UnityEngine;
using Utils;

namespace Substrate.Sectors.Elements.Hunt
{
    public enum HuntAlarmReason { Patrol, SearchClock }

    public enum PatrolMode { Route, Chase, Search }

    /// <summary>
    /// A sector's opening hunt. Patrols spawn clear of the hero and fly a looped route past the start, the
    /// sector center and back, scouts unaware so they hold fire until the alarm. One spotted meter fills while
    /// any patrol sees the hero, inside the close radius or the forward cone, clear of asteroids and not
    /// concealed, and drains while none does. A hero hit adds its share. Seeing the hero, or a hero hit, pulls
    /// that patrol off its route to chase. Losing the hero, it follows the last heading and circles there until
    /// the meter empties, then takes the nearest waypoint of its own route. The alarm sounds when the meter
    /// fills or the search clock runs out, and every live patrol wakes. The marker pulses while a patrol sees
    /// the hero. The sight wedge is faint on the route, red while it sees, and amber while it chases or searches
    /// blind. The wave director holds its opening lull until <see cref="Released"/> and counts
    /// <see cref="LivePatrols"/> against its cap. The events are plain C#, for listeners outside the sector.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class SectorHunt : SectorSpawner, ISectorRowReader
    {
        [Tooltip("Sight cone and spotted-meter timing.")]
        [Stat, SerializeField] private HuntFeelProfile feel;
        [Tooltip("The sector's faction: every patrol flies its common ship, on its kit and in its skin.")]
        [Stat, SerializeField] private SectorFaction faction;
        [Stat, SerializeField] private AICommander patrolPilot;
        [Tooltip("Material for each patrol's sight wedge on the minimap. Tinted per patrol, one instance each.")]
        [SerializeField] private Material sightWedge;
        [SerializeField] private int team = 1;

        [Header("Placement")]
        [Tooltip("Plane distances from the hero's start that patrols spawn between; author them past the sight radius.")]
        [SerializeField, Min(0f)] private float spawnDistanceMin = 100f;
        [SerializeField, Min(0f)] private float spawnDistanceMax = 150f;
        [Tooltip("A candidate point with an asteroid within this radius is passed over.")]
        [SerializeField, Min(0f)] private float clearanceRadius = 10f;
        [Tooltip("Asteroids that give cover from a patrol's sight.")]
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
        private float searchSeconds;
        private bool started;
        private bool hasTicked;
        private float startTime;
        private float lastNow;
        private float alarmAt;

        /// <summary>A patrol went from not seeing the hero to seeing it: sector index, patrol.</summary>
        public event Action<int, Ship> PatrolSpotted;

        /// <summary>The hunt ends: sector index, reason, the patrol that filled the meter (null when the search clock ran out).</summary>
        public event Action<int, HuntAlarmReason, Ship> AlarmRaised;

        /// <summary>A patrol died before the alarm: sector index, patrol.</summary>
        public event Action<int, Ship> PatrolSilenced;

        public bool Alarmed { get; private set; }

        /// <summary>True once <see cref="GraceSecondsLeft"/> has run out after the alarm. The first wave waits for it.</summary>
        public bool Released { get; private set; }

        public int LivePatrols => patrols.Count;

        public float SearchSecondsLeft { get; private set; }

        /// <summary>The spotted meter, 0 empty to 1 at the alarm. Patrols that see the hero do not stack.</summary>
        public float SpottedFill { get; private set; }

        /// <summary>Seconds from the alarm until <see cref="Released"/>. The full gap before the alarm.</summary>
        public float GraceSecondsLeft { get; private set; }

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            products.Clear();
            patrols.Clear();
            Spawned = products;
            if (!feel)
                throw new InvalidOperationException($"SectorHunt on '{name}' needs its feel profile.");
            patrolCount = ctx.Row.patrols;
            searchSeconds = ctx.Row.searchSeconds;
            if (patrolCount > 0 && (!faction || !patrolPilot || !ctx.Sector))
                throw new InvalidOperationException(
                    $"SectorHunt on '{name}' needs a faction, patrol pilot and sector when it spawns patrols.");
            if (patrolCount > 0 && !sightWedge)
                throw new InvalidOperationException(
                    $"SectorHunt on '{name}' needs its sight wedge when it spawns patrols.");

            units = ctx.Units;
            field = ctx.Field;
            hero = ctx.Hero;
            sector = ctx.Sector;
            sectorIndex = ctx.SectorIndex;
            started = false;
            hasTicked = false;
            Alarmed = false;
            Released = false;
            SpottedFill = 0f;
            SearchSecondsLeft = searchSeconds;
            GraceSecondsLeft = feel.alarmGraceSeconds;
            yield break;
        }

        public void CheckRow(SectorEntry row)
        {
            if (row.patrols < 0)
                throw new InvalidOperationException($"SectorHunt on '{name}' spawns the row's patrols, and the row asks for {row.patrols}.");
            if (row.searchSeconds < 0)
                throw new InvalidOperationException($"SectorHunt on '{name}' runs the row's search clock, and the row asks for {row.searchSeconds} s.");
        }

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            foreach (var patrol in patrols)
            {
                patrol.Ship.Damage.OnDamaged -= patrol.OnDamaged;
                patrol.Release();
                DestroyWedge(patrol);
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

            var dt = hasTicked ? now - lastNow : 0f;
            hasTicked = true;
            lastNow = now;

            DespawnDead();
            if (Alarmed)
            {
                if (!Released)
                {
                    GraceSecondsLeft = Mathf.Max(0f, alarmAt + feel.alarmGraceSeconds - now);
                    if (GraceSecondsLeft <= 0f) Released = true;
                }
                return;
            }

            var seen = false;
            foreach (var patrol in patrols)
            {
                Watch(patrol);
                if (patrol.Seeing) seen = true;
            }

            if (seen)
                SpottedFill = feel.spotFillSeconds <= 0f
                    ? 1f
                    : Mathf.Min(1f, SpottedFill + dt / feel.spotFillSeconds);
            else
                SpottedFill = feel.spotDrainSeconds <= 0f
                    ? 0f
                    : Mathf.Max(0f, SpottedFill - dt / feel.spotDrainSeconds);

            if (SpottedFill >= 1f)
            {
                Ship caller = null;
                foreach (var patrol in patrols)
                    if (patrol.Seeing) { caller = patrol.Ship; break; }
                Sound(HuntAlarmReason.Patrol, caller, now);
                return;
            }

            if (SpottedFill <= 0f)
                foreach (var patrol in patrols) patrol.ReturnToRoute();

            foreach (var patrol in patrols) patrol.Fly(feel, hero);

            SearchSecondsLeft = Mathf.Max(0f, searchSeconds - (now - startTime));
            if (SearchSecondsLeft <= 0f) Sound(HuntAlarmReason.SearchClock, null, now);
        }

        private bool Sees(Ship patrol)
        {
            if (hero.Lock.IsConcealed) return false;
            var from = patrol.transform.position;
            var to = hero.transform.position;
            var offset = GamePlane.WorldPointToPlane(to) - GamePlane.WorldPointToPlane(from);
            var distance = offset.magnitude;
            var inSight = distance <= feel.closeSightRadius || distance <= feel.sightRadius
                && Vector2.Angle(GamePlane.WorldDirToPlane(patrol.transform.up), offset) <= feel.coneDegrees * .5f;
            return inSight && LineOfSight.IsClear(from, to, hero.transform, asteroidMask);
        }

        private void Watch(Patrol patrol)
        {
            var sees = Sees(patrol.Ship);
            if (sees)
                patrol.NoteSeen(GamePlane.WorldPointToPlane(hero.transform.position));
            else if (patrol.Seeing)
                patrol.NoteHeading(PlanarVelocity(hero));

            if (sees == patrol.Seeing) return;
            patrol.Seeing = sees;
            if (sees) patrol.Chase();
            if (patrol.Rig) patrol.Rig.PulseMinimapMarker(sees);
            patrol.Show();
            if (sees) PatrolSpotted?.Invoke(sectorIndex, patrol.Ship);
        }

        private static Vector2 PlanarVelocity(Ship ship) =>
            GamePlane.WorldDirToPlane(ship.Body.linearVelocity);

        private void Sound(HuntAlarmReason reason, Ship caller, float now)
        {
            Alarmed = true;
            alarmAt = now;
            GraceSecondsLeft = feel.alarmGraceSeconds;
            Released = GraceSecondsLeft <= 0f;
            foreach (var patrol in patrols)
            {
                patrol.Release();
                patrol.Scout.Unaware = false;
                DestroyWedge(patrol);
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
                    field, placementAttempts);
                var ship = units.SpawnShip(patrolShip, patrolPilot, team, position,
                    GamePlane.PlanePose(GamePlane.Normal, heading), field,
                    new ShipLoadout(patrolShip, faction.Faction.KitFor(patrolShip), null));
                faction.Faction.Dress(patrolShip, ship, elite: false);
                var route = PatrolRoute.Waypoints(center, GamePlane.WorldPointToPlane(sector.transform.position),
                    GamePlane.WorldPointToPlane(position), feel.routeStartPass);
                var patrol = new Patrol(ship, (AICommander)ship.Commander, route);
                patrol.Scout.Unaware = true;
                patrol.Wedge = SpawnWedge(ship);
                patrol.OnDamaged = hit =>
                {
                    if (Alarmed || !hero || hit.Info.AttackerId != hero.Id) return;
                    SpottedFill = Mathf.Min(1f, SpottedFill + feel.hitFill);
                    if (!patrol.Seeing)
                        patrol.ChaseBlind(GamePlane.WorldPointToPlane(hero.transform.position), PlanarVelocity(hero));
                    if (SpottedFill >= 1f) Sound(HuntAlarmReason.Patrol, patrol.Ship, lastNow);
                };
                ship.Damage.OnDamaged += patrol.OnDamaged;
                patrols.Add(patrol);
                products.Add(ship);
            }
        }

        private PatrolSightWedge SpawnWedge(Ship ship)
        {
            var wedgeObject = new GameObject("Patrol sight");
            wedgeObject.transform.SetParent(ship.transform, false);
            var wedge = wedgeObject.AddComponent<PatrolSightWedge>();
            wedge.Build(feel, sightWedge);
            return wedge;
        }

        private static void DestroyWedge(Patrol patrol)
        {
            if (!patrol.Wedge) return;
            var wedge = patrol.Wedge.gameObject;
            patrol.Wedge = null;
            Destroy(wedge);
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
                DestroyWedge(patrol);
                if (!Alarmed) PatrolSilenced?.Invoke(sectorIndex, ship);
                units.DespawnShip(ship);
            }
        }

        private sealed class Patrol
        {
            private const float StillSpeed = 1f;
            // A short lead keeps the orbit near the circle.
            private const float OrbitLead = 40f * Mathf.Deg2Rad;

            public readonly Ship Ship;
            public readonly AICommander Commander;
            public readonly Scout Scout;
            // A presentation-less ship has no rig, or a dark one: nothing pulses.
            public readonly ShipVisualRig Rig;
            private readonly Vector2[] route;
            private int leg;
            private PatrolMode mode;
            private Vector2 lastSeen;
            private Vector2 lastHeading;
            private float lastSpeed;
            private Vector2 searchCenter;
            private bool circling;
            public Action<AppliedDamage> OnDamaged;
            public PatrolSightWedge Wedge;
            public bool Seeing;

            public Patrol(Ship ship, AICommander commander, Vector2[] route)
            {
                Ship = ship;
                Commander = commander;
                Scout = commander.Scout;
                Rig = ship.VisualRig;
                this.route = route;
            }

            public void NoteSeen(Vector2 planePoint) => lastSeen = planePoint;

            public void NoteHeading(Vector2 planarVelocity)
            {
                lastSpeed = planarVelocity.magnitude;
                if (lastSpeed > 0f)
                    lastHeading = planarVelocity / lastSpeed;
            }

            public void Chase()
            {
                mode = PatrolMode.Chase;
                circling = false;
            }

            public void ChaseBlind(Vector2 planePoint, Vector2 planarVelocity)
            {
                Chase();
                NoteSeen(planePoint);
                NoteHeading(planarVelocity);
                Show();
            }

            public void ReturnToRoute()
            {
                if (mode == PatrolMode.Route) return;
                mode = PatrolMode.Route;
                circling = false;
                var here = Here();
                var nearest = 0;
                var best = float.PositiveInfinity;
                for (var i = 0; i < route.Length; i++)
                {
                    var dist = (route[i] - here).sqrMagnitude;
                    if (dist >= best) continue;
                    best = dist;
                    nearest = i;
                }
                leg = nearest;
                Show();
            }

            public void Fly(HuntFeelProfile feel, Ship hero)
            {
                switch (mode)
                {
                    case PatrolMode.Route:
                        FlyRoute(feel.routeSpeed, feel.arrivalRadius);
                        break;
                    case PatrolMode.Chase:
                        FlyChase(feel, hero);
                        break;
                    case PatrolMode.Search:
                        FlySearch(feel);
                        break;
                    default:
                        throw new InvalidOperationException($"Patrol '{Ship.name}' has no fly for mode {mode}.");
                }
            }

            public void Show()
            {
                if (Wedge) Wedge.SetMode(mode, Seeing);
            }

            private void FlyChase(HuntFeelProfile feel, Ship hero)
            {
                if (Seeing)
                {
                    Commander.SteerTo(GamePlane.WorldPointToPlane(hero.transform.position), feel.seekSpeed);
                    return;
                }

                var here = Here();
                if ((lastSeen - here).sqrMagnitude <= feel.arrivalRadius * feel.arrivalRadius)
                {
                    BeginSearch(feel, here);
                    FlySearch(feel);
                    return;
                }

                Commander.SteerTo(lastSeen, feel.seekSpeed);
            }

            private void BeginSearch(HuntFeelProfile feel, Vector2 here)
            {
                mode = PatrolMode.Search;
                searchCenter = lastSpeed < StillSpeed ? lastSeen : lastSeen + lastHeading * feel.followDistance;
                circling = (searchCenter - here).sqrMagnitude <= feel.arrivalRadius * feel.arrivalRadius;
                Show();
            }

            private void FlySearch(HuntFeelProfile feel)
            {
                var here = Here();
                if (!circling)
                {
                    if ((searchCenter - here).sqrMagnitude <= feel.arrivalRadius * feel.arrivalRadius)
                        circling = true;
                    else
                    {
                        Commander.SteerTo(searchCenter, feel.seekSpeed);
                        return;
                    }
                }

                var offset = here - searchCenter;
                var bearing = Mathf.Atan2(offset.y, offset.x);
                var ahead = bearing + OrbitLead;
                Commander.SteerTo(
                    searchCenter + new Vector2(Mathf.Cos(ahead), Mathf.Sin(ahead)) * feel.searchRadius,
                    feel.seekSpeed);
            }

            private Vector2 Here() => GamePlane.WorldPointToPlane(Ship.transform.position);

            private void FlyRoute(float speed, float arrivalRadius)
            {
                var here = Here();
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
            float spawnDistance, Material sightWedge = null)
        {
            this.feel = feel;
            this.faction = faction;
            this.patrolPilot = patrolPilot;
            spawnDistanceMin = spawnDistanceMax = spawnDistance;
            this.sightWedge = sightWedge;
        }
#endif
    }
}
