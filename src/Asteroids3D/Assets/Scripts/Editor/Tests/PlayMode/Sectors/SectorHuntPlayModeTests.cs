#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using AI;
using AI.Scanning;
using Combat.Weapons;
using Damage;
using NUnit.Framework;
using Objectives;
using Objectives.Presentation;
using Ships;
using Ships.Visuals;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Factions;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Substrate.Sessions;

namespace Tests.PlayMode.Sectors
{
    /// <summary>A sector's hunt with real patrols: unaware, flying a route that brings one into sight of a still hero. Sight fills one meter, the marker pulses and the wedge turns while a patrol sees the hero, and the alarm waits until the meter is full. A patrol that sees the hero leaves its route to chase while the others keep theirs. Blind, it steers to the last-seen point past asteroids, follows the last heading and circles there, chases again on sight, and returns to its nearest waypoint when the meter empties. It holds fire until the alarm. A hit adds its share. The gap after the alarm holds the opening lull, then phase one opens. Asteroids and the cone hide the hero, breaking sight drains the meter, and killing a patrol leaves the meter to drain so the search clock ends the hunt.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorHuntPlayModeTests : PlayModeWorldFixture
    {
        private const string PatrolPilotPath = "Assets/Prefabs/Pilots/AggressorPilot.prefab";
        private const string PatrolKitPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const float SpawnDistance = 30f;
        private const int SectorIndex = 1;

        private UnitService _unitService;
        private ObjectiveService _objectives;
        private readonly List<Object> _created = new();
        private readonly List<string> _beats = new();
        private int _patrols;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            var unitServiceGO = Track(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();
            _objectives = Track(new GameObject("ObjectiveService")).AddComponent<ObjectiveService>();
            ShipServices.Compose(_unitService, unitServiceGO.transform, presentationEnabled: true);
        }

        [TearDown]
        public override void TearDown()
        {
            if (_unitService) _unitService.Clear();
            foreach (var item in _created)
                if (item) Object.DestroyImmediate(item);
            _created.Clear();
            _beats.Clear();
            base.TearDown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator PatrolThatSeesTheHero_FillsTheMeter_ThenTheGapOpensPhaseOne()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 20f, searchSeconds: 60f, spotFillSeconds: .4f, alarmGraceSeconds: .5f,
                routeSpeed: 0f);
            var sectorGO = Track(new GameObject("HuntedSector"));
            hunt.transform.SetParent(sectorGO.transform, false);
            var director = Child(sectorGO, "WaveDirector").AddComponent<WaveDirector>();
            var row = HuntRow(new SectorPhase { seconds = 30f, cap = 0, groups = System.Array.Empty<WaveGroup>() });
            director.Configure(row.phases, openingLullSeconds: 5f, hunt: hunt);
            var spine = sectorGO.AddComponent<PhaseSpineModule>();
            spine.Bind(director, NewZone(sectorGO, "Zone"), NewZone(sectorGO, "Beacon"), hunt: hunt);
            var sector = sectorGO.AddComponent<Sector>();
            sector.SetManifest(null, new SectorSpawner[] { director, hunt }, new SectorModule[] { spine });
            sector.Initialize(_unitService, _objectives, true, default, hero, row, SectorIndex);

            yield return sector.Setup();
            yield return null;
            yield return null;
            var patrol = Patrol(hunt);
            var wedge = patrol.GetComponentInChildren<PatrolSightWedge>();
            Assert.AreEqual(SpawnDistance, PlaneDistance(patrol, hero), 2f, "The patrol spawns its distance from the start.");
            Assert.IsTrue(Scout(patrol).Unaware, "A patrol flies unaware of the hero.");
            Assert.IsTrue(Commander(patrol).Steered, "An unaware patrol flies its route.");
            Assert.AreEqual(HuntReading.Searching, Progress().Hunt, "The countdown reads the search.");
            Assert.IsTrue(director.Phase.InOpeningLull);
            Assert.IsEmpty(_objectives.Finds, "A patrol is no find, so it gets no minimap edge pointer.");
            Assert.IsTrue(wedge, "Each patrol carries a sight wedge.");
            Assert.AreEqual(LayerIds.MinimapEnemy, wedge.gameObject.layer);

            ShipTestFactory.MoveKinematicShip(hero, patrol.transform.position + GamePlane.Right * 10f);
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {patrol.name}" }, _beats);
            Assert.Greater(hunt.SpottedFill, 0f);
            Assert.Less(hunt.SpottedFill, 1f, "Sight fills the meter. It does not alarm on the first tick.");
            Assert.IsFalse(hunt.Alarmed);
            Assert.AreEqual(HuntReading.Spotted, Progress().Hunt, "The countdown reads the spotted meter.");
            Assert.That(Progress().SecondsLeft, Is.GreaterThan(50f), "Spotted still counts the search clock.");
            Assert.AreEqual(hunt.SpottedFill, Progress().SpottedFill, 1e-4f);
            Assert.IsTrue(Scout(patrol).Unaware, "A seen patrol still holds fire.");
            Assert.IsTrue(Marker(patrol).Pulsing, "The patrol's marker pulses while it sees the hero.");

            yield return WaitSeconds(() => hunt.Alarmed, 3f);
            Assert.AreEqual(1f, hunt.SpottedFill, 1e-3f, "The alarm waits until the meter is full.");
            Assert.IsFalse(hunt.Released);
            Assert.IsTrue(director.Phase.InOpeningLull, "The opening lull holds through the gap.");
            Assert.AreEqual(HuntReading.Coming, Progress().Hunt);
            Assert.That(Progress().SecondsLeft, Is.GreaterThan(0f).And.LessThanOrEqualTo(.5f));
            Assert.IsFalse(Scout(patrol).Unaware, "The alarm wakes the patrol.");
            Assert.IsFalse(Commander(patrol).Steered, "The alarm hands the patrol back to its brain.");
            Assert.IsFalse(Marker(patrol).Pulsing, "The alarm stops the pulse.");

            yield return WaitSeconds(() => hunt.Released && director.Phase.Index == 0 && !director.Phase.InLull, 3f);
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {patrol.name}", $"alarm {SectorIndex} Patrol {patrol.name}" },
                _beats);
            Assert.IsFalse(director.Phase.InLull, "Phase one opens when the gap ends.");
            Assert.AreEqual(0, director.Phase.Index, "The hunt adds no sector phase.");
            Assert.IsFalse(wedge, "The alarm retires the sight wedge.");
            Assert.AreEqual(HuntReading.None, Progress().Hunt);

            yield return sector.Teardown();
            yield return null;
            Assert.IsFalse(patrol, "Teardown despawns the patrols.");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator KillingAPatrolThatSeesTheHero_LeavesTheMeterToDrain_AndTheSearchClockEndsTheHunt()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 50f, searchSeconds: 8f, spotFillSeconds: 2f, spotDrainSeconds: 4f,
                hitFill: 0f, alarmGraceSeconds: 30f);
            yield return Build(hunt, hero);
            yield return null;
            yield return null;
            var patrol = Patrol(hunt);
            var name = patrol.name;
            var marker = Marker(patrol);
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {name}" }, _beats, "A patrol in sight has seen the hero.");
            Assert.IsTrue(marker.Pulsing);

            yield return WaitSeconds(() => hunt.SpottedFill > .35f, 3f);
            Assert.IsFalse(hunt.Alarmed);
            var filled = hunt.SpottedFill;

            patrol.Damage.TakeDamage(new DamageInfo(1e6f, DamageKind.Laser, hero.Id, 0f, Vector3.zero, patrol.transform.position));
            yield return null;
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {name}", $"silenced {SectorIndex} {name}" }, _beats);
            Assert.IsEmpty(hunt.Spawned, "A dead patrol is despawned.");
            Assert.IsFalse(marker.Pulsing, "The dead patrol stops pulsing.");
            Assert.Greater(hunt.SpottedFill, .2f, "Killing the patrol leaves the meter to drain.");
            Assert.Less(hunt.SpottedFill, filled);

            yield return WaitSeconds(() => hunt.Alarmed, 10f);
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {name}", $"silenced {SectorIndex} {name}", $"alarm {SectorIndex} SearchClock none" },
                _beats, "The meter never fills. The search clock sounds the alarm.");
            Assert.IsFalse(hunt.Released, "The search-clock alarm gets the same gap.");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator AnAsteroidBetween_HidesTheHero_UntilItIsGone()
        {
            var hero = SpawnHero();
            var rock = Track(new GameObject("Rock"));
            rock.layer = 8;
            rock.AddComponent<SphereCollider>().radius = 15f;
            Physics.SyncTransforms();
            var hunt = NewHunt(sightRadius: 50f, searchSeconds: 60f);
            yield return Build(hunt, hero);
            yield return WaitSeconds(() => false, .5f);
            Assert.IsEmpty(_beats, "A patrol sees no hero behind a rock.");

            Object.DestroyImmediate(rock);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {Patrol(hunt).name}" }, _beats);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator AHeroBehindThePatrol_IsUnseenBeyondCloseSight_AndSeenInsideIt()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 50f, searchSeconds: 60f, spawnDistance: 80f, coneDegrees: 90f,
                closeSightRadius: 12f, spotFillSeconds: 30f);
            yield return Build(hunt, hero);
            yield return null;
            yield return null;
            var patrol = Patrol(hunt);
            Assert.Greater(PlaneDistance(patrol, hero), 50f, "The patrol spawns out of sight.");
            Assert.IsEmpty(_beats);

            Face(patrol, GamePlane.Forward);
            ShipTestFactory.MoveKinematicShip(hero, patrol.transform.position - GamePlane.Forward * 20f);
            hunt.Tick(Time.time);
            Assert.IsEmpty(_beats, "Behind the cone, and outside the close radius, the patrol does not see the hero.");
            Assert.AreEqual(0f, hunt.SpottedFill);

            ShipTestFactory.MoveKinematicShip(hero, patrol.transform.position - GamePlane.Forward * 6f);
            hunt.Tick(Time.time + .05f);
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {patrol.name}" }, _beats,
                "Inside the close radius the patrol sees the hero in every direction.");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator SightBreak_DrainsTheMeter_AndDoesNotAlarm()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 20f, searchSeconds: 30f, closeSightRadius: 0f, spotFillSeconds: .6f,
                spotDrainSeconds: .6f, alarmGraceSeconds: 10f, routeSpeed: 0f);
            yield return Build(hunt, hero);
            yield return null;
            yield return null;
            var patrol = Patrol(hunt);

            ShipTestFactory.MoveKinematicShip(hero, patrol.transform.position + GamePlane.Right * 10f);
            yield return WaitSeconds(() => hunt.SpottedFill > .45f, 3f);
            Assert.IsFalse(hunt.Alarmed, "A part-filled meter is not the alarm.");
            var filled = hunt.SpottedFill;

            ShipTestFactory.MoveKinematicShip(hero, patrol.transform.position + GamePlane.Right * 80f);
            yield return WaitSeconds(() => hunt.SpottedFill < .15f, 3f);
            Assert.Less(hunt.SpottedFill, filled, "Breaking sight drains the meter.");
            Assert.IsFalse(hunt.Alarmed);
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {patrol.name}" }, _beats);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator AHitAddsItsShare_AndAFullShareAlarmsOnOneHit()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 5f, searchSeconds: 60f, hitFill: .5f);
            yield return Build(hunt, hero);
            yield return null;
            var patrol = Patrol(hunt);
            Assert.IsEmpty(_beats);

            var hit = new DamageInfo(1f, DamageKind.Laser, hero.Id, 0f, Vector3.zero, patrol.transform.position);
            patrol.Damage.TakeDamage(hit);
            Assert.AreEqual(.5f, hunt.SpottedFill, 1e-4f, "A hero hit adds its share of the meter.");
            Assert.IsFalse(hunt.Alarmed);
            Assert.IsEmpty(_beats, "A hit out of sight does not count as seeing.");

            patrol.Damage.TakeDamage(hit);
            Assert.AreEqual(1f, hunt.SpottedFill, 1e-4f);
            Assert.IsTrue(hunt.Alarmed);
            Assert.AreEqual(new[] { $"alarm {SectorIndex} Patrol {patrol.name}" }, _beats);

            _beats.Clear();
            var full = NewHunt(sightRadius: 5f, searchSeconds: 60f, hitFill: 1f);
            yield return Build(full, hero);
            yield return null;
            var other = Patrol(full);
            other.Damage.TakeDamage(new DamageInfo(1f, DamageKind.Laser, hero.Id, 0f, Vector3.zero, other.transform.position));
            Assert.IsTrue(full.Alarmed, "A full share alarms on one hit.");
            Assert.AreEqual(new[] { $"alarm {SectorIndex} Patrol {other.name}" }, _beats);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator AnUnawarePatrolFarOut_FliesItsRouteIntoSightOfAStillHero()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 50f, searchSeconds: 60f, spawnDistance: 120f);
            yield return Build(hunt, hero);
            yield return null;
            yield return null;
            Assert.IsEmpty(_beats, "The patrol spawns out of sight.");

            yield return WaitSeconds(() => _beats.Count > 0, 15f);
            var patrol = Patrol(hunt);
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {patrol.name}" }, _beats,
                "The route brings the patrol into sight of the start.");
            Assert.IsTrue(Scout(patrol).Unaware, "It holds fire while it chases.");
            var gap = PlaneDistance(patrol, hero);
            yield return WaitSeconds(() => PlaneDistance(patrol, hero) < gap - 4f, 8f);
            Assert.Less(PlaneDistance(patrol, hero), gap, "Seeing the hero pulls the patrol off its route.");
            Assert.IsTrue(Commander(patrol).Steered);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator LivePatrols_TakeTheDirectorsSlots_AndADeadOneFreesItsSlot()
        {
            const int cap = 3;
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 5f, searchSeconds: .5f, patrols: 2);
            var template = ShipTestFactory.CreateKinematicPrimitiveShipAt(new Vector2(500f, 500f));
            Track(template.gameObject);
            var sectorGO = Track(new GameObject("HuntedSector"));
            hunt.transform.SetParent(sectorGO.transform, false);
            var director = Child(sectorGO, "WaveDirector").AddComponent<WaveDirector>();
            var row = HuntRow(new SectorPhase
            {
                seconds = 60f,
                cap = cap,
                endsAt = 0,
                breather = 0f,
                longestWait = 0.25f,
                groups = new[]
                {
                    new WaveGroup { ships = new[] { new WaveDirector.RosterEntry { template = template } } },
                },
            });
            director.Configure(row.phases, openingLullSeconds: 5f, hunt: hunt);
            var sector = sectorGO.AddComponent<Sector>();
            sector.SetManifest(null, new SectorSpawner[] { director, hunt }, null);
            sector.Initialize(_unitService, _objectives, true, default, hero, row, SectorIndex);

            yield return sector.Setup();
            yield return WaitSeconds(() => director.Spawned.Count > 0, 3f);
            Assert.IsTrue(hunt.Alarmed);
            Assert.AreEqual(2, hunt.LivePatrols);
            Assert.AreEqual(cap - 2, director.Spawned.Count, "The opening wave takes the one slot the patrols leave.");

            yield return WaitSeconds(() => false, 1f);
            Assert.AreEqual(cap - 2, director.Spawned.Count, "No wave passes the ceiling while both patrols live.");

            var patrol = hunt.Spawned[0];
            patrol.Damage.TakeDamage(new DamageInfo(1e6f, DamageKind.Laser, hero.Id, 0f, Vector3.zero, patrol.transform.position));
            yield return WaitSeconds(() => director.Spawned.Count > cap - 2, 2f);
            Assert.AreEqual(1, hunt.LivePatrols, "The dead patrol stops counting.");
            Assert.AreEqual(cap - 1, director.Spawned.Count, "The dead patrol's slot goes to the next wave.");

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator APatrolThatSeesTheHero_LeavesItsRoute_AndOneThatNeverSawStays()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 50f, searchSeconds: 60f, spawnDistance: 150f, patrols: 2,
                coneDegrees: 360f, closeSightRadius: 0f, spotFillSeconds: 20f, spotDrainSeconds: 20f,
                routeSpeed: 10f, seekSpeed: 18f);
            yield return Build(hunt, hero);
            yield return null;
            Assert.AreEqual(2, hunt.Spawned.Count);
            var seen = hunt.Spawned[0];
            var other = hunt.Spawned[1];
            seen.name = "Seen";
            other.name = "Other";

            MoveShip(seen, GamePlane.PlanePointToWorld(new Vector2(0f, 40f)));
            Face(seen, GamePlane.Forward);
            MoveShip(other, GamePlane.PlanePointToWorld(new Vector2(260f, 0f)));
            Face(other, GamePlane.Right);
            ShipTestFactory.MoveKinematicShip(hero, GamePlane.PlanePointToWorld(new Vector2(0f, 78f)));
            var otherStart = other.transform.position;
            yield return null;
            yield return null;

            Assert.AreEqual(new[] { $"spotted {SectorIndex} Seen" }, _beats, "Only the patrol that sees the hero leaves its route.");
            Assert.Greater(WedgeColor(seen).a, .6f, "Its wedge is red while it sees the hero.");
            Assert.Less(WedgeColor(other).a, .25f, "The other patrol's wedge stays faint on the route.");
            Assert.IsTrue(Scout(seen).Unaware, "A chasing patrol holds fire.");
            Assert.IsTrue(Scout(other).Unaware, "A patrol on its route holds fire.");

            var gap = PlaneDistance(seen, hero);
            yield return WaitSeconds(() => PlaneDistance(seen, hero) < 18f, 10f);
            Assert.Less(PlaneDistance(seen, hero), 18f, "The patrol that sees the hero closes in.");
            Assert.Less(PlaneDistance(seen, hero), gap, "It leaves the route toward the hero.");
            Assert.Greater(PlaneDistance(other, hero), 180f, "The patrol that never saw does not chase.");
            Assert.Greater(Vector3.Distance(other.transform.position, otherStart), 4f, "It keeps flying its route.");
            Assert.AreEqual(1, _beats.Count);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AfterSightBreaks_ItSteersToTheFrozenLastSeenPoint()
        {
            var hero = SpawnHero();
            var hunt = SeekingHunt();
            yield return Build(hunt, hero);
            yield return null;
            var patrol = Patrol(hunt);
            var seenAt = new Vector2(0f, 0f);
            yield return SpotThenHide(hunt, hero, patrol, seenAt, Vector2.zero, 48f);

            ShipTestFactory.MoveKinematicShip(hero, GamePlane.PlanePointToWorld(new Vector2(-400f, -220f)));
            DriveHero(hero, Vector2.zero);
            yield return null;
            Assert.Greater(WedgeColor(patrol).g, .5f, "The wedge is amber while it chases without sight.");
            Assert.That(WedgeColor(patrol).a, Is.GreaterThan(.4f).And.LessThan(.6f));

            var before = PlaneOf(patrol);
            var toSeen = _lastSeen - before;
            yield return new WaitForSeconds(1.5f);
            var moved = PlaneOf(patrol) - before;
            Assert.Greater(moved.magnitude, 8f, "It keeps steering after sight breaks.");
            Assert.Greater(Vector2.Dot(moved.normalized, toSeen.normalized), .75f,
                "It steers to the frozen last-seen point.");
            Assert.Less(Vector2.Dot(moved.normalized, (PlaneOf(hero) - before).normalized), .35f,
                "Moving the hero afterwards does not move the point.");
            Assert.IsTrue(Scout(patrol).Unaware, "It holds fire while it chases blind.");
            Assert.IsFalse(hunt.Alarmed);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ReachingTheLastSeenPoint_ItFollowsTheHeading_ThenOrbits()
        {
            const float follow = 36f;
            const float radius = 25f;
            var hero = SpawnHero();
            var hunt = SeekingHunt(followDistance: follow, searchRadius: radius, arrivalRadius: 10f);
            yield return Build(hunt, hero);
            yield return null;
            var patrol = Patrol(hunt);
            yield return SpotThenHide(hunt, hero, patrol, Vector2.zero, new Vector2(14f, 0f), 24f);
            Assert.Greater(_headingSpeed, 1f, "The break keeps the hero's planar speed.");
            var center = _lastSeen + _heading * follow;
            var midpoint = _lastSeen + _heading * (follow * .5f);

            yield return WaitSeconds(() => Distance(patrol, midpoint) < 16f, 12f);
            Assert.Less(Distance(patrol, midpoint), 18f, "It flies on along the last heading.");
            yield return WaitSeconds(() => Mathf.Abs(Vector2.Distance(PlaneOf(patrol), center) - radius) < 9f, 14f);
            Assert.AreEqual(radius, Vector2.Distance(PlaneOf(patrol), center), 10f, "It orbits the search center.");
            var start = BearingDegrees(PlaneOf(patrol) - center);
            yield return WaitSeconds(() => Mathf.DeltaAngle(start, BearingDegrees(PlaneOf(patrol) - center)) > 25f, 8f);
            Assert.Greater(Mathf.DeltaAngle(start, BearingDegrees(PlaneOf(patrol) - center)), 25f,
                "The orbit keeps turning around the search center.");
            Assert.IsTrue(Scout(patrol).Unaware, "It holds fire while it searches.");
            Assert.IsFalse(hunt.Alarmed);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SeeingTheHeroAgainWhileSearching_ReturnsToTheChase()
        {
            const float follow = 40f;
            var hero = SpawnHero();
            var hunt = SeekingHunt(followDistance: follow, sightRadius: 50f);
            yield return Build(hunt, hero);
            yield return null;
            var patrol = Patrol(hunt);
            yield return SpotThenHide(hunt, hero, patrol, Vector2.zero, new Vector2(14f, 0f), 22f);
            Assert.Greater(_headingSpeed, 1f, "The break keeps the hero's planar speed.");

            yield return WaitSeconds(() => Vector2.Dot(PlaneOf(patrol) - _lastSeen, _heading) > 12f, 10f);
            Assert.Greater(Vector2.Dot(PlaneOf(patrol) - _lastSeen, _heading), 12f, "It is out along the search.");
            Assert.Greater(PlaneDistance(patrol, hero), 200f);

            var nose = GamePlane.WorldDirToPlane(patrol.transform.up);
            Assert.Greater(nose.sqrMagnitude, .25f, "The patrol has a nose to place the hero beside.");
            nose.Normalize();
            var flank = new Vector2(-nose.y, nose.x);
            ShipTestFactory.MoveKinematicShip(hero, GamePlane.PlanePointToWorld(PlaneOf(patrol) + flank * 35f));
            DriveHero(hero, Vector2.zero);
            yield return null;
            yield return null;
            Assert.AreEqual(2, _beats.Count, "Seeing the hero again spots them.");
            var gap = PlaneDistance(patrol, hero);
            yield return WaitSeconds(() => PlaneDistance(patrol, hero) < gap - 4f, 8f);
            Assert.Less(PlaneDistance(patrol, hero), gap - 3f, "It chases the hero again.");
            Assert.IsTrue(Scout(patrol).Unaware, "It still holds fire.");
            Assert.IsFalse(hunt.Alarmed);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator WhenTheMeterDrains_ItReturnsToTheNearestWaypoint()
        {
            const float pass = 25f;
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 40f, searchSeconds: 40f, spawnDistance: 100f, coneDegrees: 360f,
                closeSightRadius: 0f, spotFillSeconds: .5f, spotDrainSeconds: 1.5f, routeSpeed: 20f, seekSpeed: 0f,
                arrivalRadius: 12f, routeStartPass: pass);
            yield return Build(hunt, hero);
            yield return null;
            var patrol = Patrol(hunt);
            var route = PatrolRoute.Waypoints(Vector2.zero, Vector2.zero, PlaneOf(patrol), pass);

            Face(patrol, GamePlane.Right);
            ShipTestFactory.MoveKinematicShip(hero, patrol.transform.position + GamePlane.Right * 16f);
            yield return WaitSeconds(() => hunt.SpottedFill > .6f, 3f);
            Assert.Greater(hunt.SpottedFill, .6f, "Sight fills the meter before the break.");
            ShipTestFactory.MoveKinematicShip(hero, GamePlane.PlanePointToWorld(new Vector2(0f, 400f)));
            var along = (route[1] - route[2]).normalized;
            var side = new Vector2(-along.y, along.x);
            var far = route[2] + side * 90f;
            MoveShip(patrol, GamePlane.PlanePointToWorld(far));
            var nearest = NearestWaypoint(route, far);
            Assert.Greater(SecondNearest(route, far), Vector2.Distance(far, nearest) + 25f,
                "The stand-off has one clear nearest waypoint.");

            yield return WaitSeconds(() => hunt.SpottedFill <= 0f, 5f);
            Assert.AreEqual(0f, hunt.SpottedFill, 1e-3f);
            Assert.IsFalse(hunt.Alarmed);
            Assert.IsTrue(Scout(patrol).Unaware, "An empty meter sends it home still holding fire.");
            var before = PlaneOf(patrol);
            yield return WaitSeconds(() => Vector2.Distance(PlaneOf(patrol), before) > 12f, 8f);
            var moved = PlaneOf(patrol) - before;
            Assert.Greater(moved.magnitude, 12f);
            Assert.Greater(Vector2.Dot(moved.normalized, (nearest - before).normalized), .8f,
                "It heads for the nearest waypoint on its route.");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AnAsteroidBetween_DoesNotStopTheChaseToTheLastSeenPoint()
        {
            var hero = SpawnHero();
            var hunt = SeekingHunt(arrivalRadius: 12f);
            var rocks = new PlacedRock();
            yield return Build(hunt, hero, rocks);
            yield return null;
            var patrol = Patrol(hunt);
            yield return SpotThenHide(hunt, hero, patrol, new Vector2(70f, 0f), Vector2.zero, 20f);
            var rock = Rock(Vector2.zero, 8f);
            rocks.Collider = rock.GetComponent<Collider>();
            rocks.Radius = 8f;
            MoveShip(patrol, GamePlane.PlanePointToWorld(new Vector2(-80f, 0f)));

            yield return WaitSeconds(() => Distance(patrol, _lastSeen) < 16f, 22f);
            Assert.Less(Distance(patrol, _lastSeen), 18f, "It reaches the last-seen point with an asteroid in the way.");
            Assert.IsTrue(Scout(patrol).Unaware, "It holds fire through the chase.");
            Assert.IsFalse(hunt.Alarmed);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AnAsteroidOnTheSearchLoop_DoesNotStallTheOrbit()
        {
            const float follow = 30f;
            const float radius = 25f;
            var hero = SpawnHero();
            var hunt = SeekingHunt(followDistance: follow, searchRadius: radius, arrivalRadius: 10f);
            var rocks = new PlacedRock();
            yield return Build(hunt, hero, rocks);
            yield return null;
            var patrol = Patrol(hunt);
            yield return SpotThenHide(hunt, hero, patrol, Vector2.zero, new Vector2(14f, 0f), 22f);
            Assert.Greater(_headingSpeed, 1f, "The break keeps the hero's planar speed.");
            var center = _lastSeen + _heading * follow;

            yield return WaitSeconds(() => Mathf.Abs(Vector2.Distance(PlaneOf(patrol), center) - radius) < 8f, 18f);
            var offset = PlaneOf(patrol) - center;
            var bearing = Mathf.Atan2(offset.y, offset.x);
            var rockBearing = bearing + 75f * Mathf.Deg2Rad;
            var rockPlane = center + new Vector2(Mathf.Cos(rockBearing), Mathf.Sin(rockBearing)) * (radius - 1f);
            var rock = Rock(rockPlane, 4f);
            rocks.Collider = rock.GetComponent<Collider>();
            rocks.Radius = 4f;

            var advanced = 0f;
            var prev = bearing * Mathf.Rad2Deg;
            var stall = 0f;
            var stallStart = -1f;
            var passed = false;
            var deadline = Time.time + 18f;
            while (Time.time < deadline)
            {
                offset = PlaneOf(patrol) - center;
                var now = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                advanced += Mathf.DeltaAngle(prev, now);
                prev = now;
                if (patrol.Body.linearVelocity.magnitude < 1f)
                {
                    if (stallStart < 0f) stallStart = Time.time;
                    stall = Mathf.Max(stall, Time.time - stallStart);
                }
                else stallStart = -1f;
                if (advanced > 105f)
                {
                    passed = true;
                    break;
                }
                yield return null;
            }

            Assert.IsTrue(passed, "Its bearing from the search center advances past the asteroid.");
            Assert.Less(stall, 2.1f, "It does not stall against the asteroid.");
            Assert.IsTrue(Scout(patrol).Unaware, "It holds fire through the search.");
            Assert.IsFalse(hunt.Alarmed);
        }

        private Ship SpawnHero() => Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();

        [UnityTest]
        public IEnumerator Produce_WithoutASightWedge_NamesTheMissingRef()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 20f, searchSeconds: 60f, sightWedge: false);
            var build = Build(hunt, hero);
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                while (build.MoveNext()) { }
            });
            StringAssert.Contains("sight wedge", ex.Message);
            yield break;
        }

        private SectorHunt SeekingHunt(float followDistance = 30f, float searchRadius = 25f, float arrivalRadius = 12f,
            float sightRadius = 60f) =>
            NewHunt(sightRadius: sightRadius, searchSeconds: 120f, spawnDistance: 140f, coneDegrees: 360f,
                closeSightRadius: 0f, spotFillSeconds: 1f, spotDrainSeconds: 10000f, routeSpeed: 0f, seekSpeed: 18f,
                followDistance: followDistance, searchRadius: searchRadius, arrivalRadius: arrivalRadius);

        private SectorHunt NewHunt(float sightRadius, float searchSeconds,
            float spawnDistance = SpawnDistance, int patrols = 1, float coneDegrees = 360f,
            float closeSightRadius = 12f, float spotFillSeconds = 30f, float spotDrainSeconds = 3f,
            float hitFill = .5f, float alarmGraceSeconds = 0f, float routeSpeed = 8f, float seekSpeed = 14f,
            float followDistance = 30f, float searchRadius = 25f, float arrivalRadius = 15f, float routeStartPass = 25f,
            bool sightWedge = true)
        {
            var feel = Track(ScriptableObject.CreateInstance<HuntFeelProfile>());
            feel.sightRadius = sightRadius;
            feel.searchSeconds = searchSeconds;
            feel.coneDegrees = coneDegrees;
            feel.closeSightRadius = closeSightRadius;
            feel.spotFillSeconds = spotFillSeconds;
            feel.spotDrainSeconds = spotDrainSeconds;
            feel.hitFill = hitFill;
            feel.alarmGraceSeconds = alarmGraceSeconds;
            feel.routeSpeed = routeSpeed;
            feel.seekSpeed = seekSpeed;
            feel.followDistance = followDistance;
            feel.searchRadius = searchRadius;
            feel.arrivalRadius = arrivalRadius;
            feel.routeStartPass = routeStartPass;
            _patrols = patrols;
            var hunt = Track(new GameObject("Hunt")).AddComponent<SectorHunt>();
            var faction = Track(ScriptableObject.CreateInstance<Faction>());
            faction.common = new FactionShip
            {
                ship = TestAssets.LoadCrimsonPrefab(), kit = TestAssets.Load<WeaponComponent>(PatrolKitPath),
            };
            var sectorFaction = Track(new GameObject("Faction")).AddComponent<SectorFaction>();
            sectorFaction.Bind(faction);
            var wedge = sightWedge
                ? TestAssets.Load<Material>("Assets/Visuals/Ui/Minimap/UI_Minimap_Enemy.mat")
                : null;
            hunt.Configure(feel, sectorFaction, TestAssets.Load<AICommander>(PatrolPilotPath), spawnDistance, wedge);
            hunt.PatrolSpotted += (sector, patrol) => _beats.Add($"spotted {sector} {patrol.name}");
            hunt.PatrolSilenced += (sector, patrol) => _beats.Add($"silenced {sector} {patrol.name}");
            hunt.AlarmRaised += (sector, reason, caller) =>
                _beats.Add($"alarm {sector} {reason} {(caller ? caller.name : "none")}");
            return hunt;
        }

        private SectorEntry HuntRow(SectorPhase phase = default) => new()
        {
            phases = new[] { phase },
            patrols = _patrols
        };

        private IEnumerator Build(SectorHunt hunt, Ship hero, IObstacleField field = null) =>
            hunt.Build(new SectorBuildContext(_unitService, _objectives, true,
                Track(new GameObject("Sector")).AddComponent<Sector>(), default, field: field, hero: hero,
                sectorIndex: SectorIndex, row: HuntRow()));

        private static Ship Patrol(SectorHunt hunt)
        {
            Assert.AreEqual(1, hunt.Spawned.Count, "The hunt spawns its patrol on its first tick.");
            return hunt.Spawned[0];
        }

        private static Scout Scout(Ship patrol) => Commander(patrol).Scout;

        private static AICommander Commander(Ship patrol) => (AICommander)patrol.Commander;

        private static MinimapShipMarker Marker(Ship patrol)
        {
            var marker = patrol.GetComponentInChildren<MinimapShipMarker>(true);
            Assert.IsTrue(marker, "The patrol ship carries a minimap marker.");
            return marker;
        }

        private SectorPhaseProgress Progress() => _objectives.SpineTracker.PhaseProgress.Value;

        private static void Face(Ship ship, Vector3 heading)
        {
            var rotation = GamePlane.PlanePose(GamePlane.Normal, heading);
            ship.transform.rotation = rotation;
            ship.Body.rotation = rotation;
            ship.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static float PlaneDistance(Ship a, Ship b) => Vector2.Distance(PlaneOf(a), PlaneOf(b));

        private static Vector2 PlaneOf(Ship ship) => GamePlane.WorldPointToPlane(ship.transform.position);

        private static float Distance(Ship ship, Vector2 plane) => Vector2.Distance(PlaneOf(ship), plane);

        private static float BearingDegrees(Vector2 offset) => Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

        private static void MoveShip(Ship ship, Vector3 position)
        {
            ship.transform.position = position;
            ship.Body.position = position;
            ship.Body.linearVelocity = Vector3.zero;
            ship.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            ship.KinematicsPoller.Poll();
        }

        private static void DriveHero(Ship hero, Vector2 planar)
        {
            hero.Body.isKinematic = planar == Vector2.zero;
            hero.Body.useGravity = false;
            hero.Body.linearVelocity = GamePlane.PlaneDirToWorld(planar);
            if (planar != Vector2.zero)
                hero.Body.constraints |= RigidbodyConstraints.FreezeRotation | GamePlane.PositionConstraint;
        }

        private static Color WedgeColor(Ship patrol)
        {
            var wedge = patrol.GetComponentInChildren<PatrolSightWedge>();
            Assert.IsTrue(wedge, "The patrol carries a sight wedge.");
            return wedge.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_Color");
        }

        private static Vector2 NearestWaypoint(Vector2[] route, Vector2 here)
        {
            var best = route[0];
            var bestDist = float.PositiveInfinity;
            foreach (var point in route)
            {
                var dist = (point - here).sqrMagnitude;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = point;
            }
            return best;
        }

        private static float SecondNearest(Vector2[] route, Vector2 here)
        {
            var best = float.PositiveInfinity;
            var second = float.PositiveInfinity;
            foreach (var point in route)
            {
                var dist = Vector2.Distance(here, point);
                if (dist < best)
                {
                    second = best;
                    best = dist;
                }
                else if (dist < second)
                    second = dist;
            }
            return second;
        }

        private GameObject Rock(Vector2 plane, float radius)
        {
            var rock = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            rock.name = "Rock";
            rock.layer = 8;
            rock.transform.localScale = Vector3.one * (radius * 2f);
            rock.transform.position = GamePlane.PlanePointToWorld(plane);
            Physics.SyncTransforms();
            return rock;
        }

        private Vector2 _lastSeen;
        private Vector2 _heading;
        private float _headingSpeed;

        private IEnumerator SpotThenHide(SectorHunt hunt, Ship hero, Ship patrol, Vector2 heroAt, Vector2 velocity,
            float standoff)
        {
            MoveShip(patrol, GamePlane.PlanePointToWorld(heroAt - new Vector2(standoff, 0f)));
            Face(patrol, GamePlane.Right);
            ShipTestFactory.MoveKinematicShip(hero, GamePlane.PlanePointToWorld(heroAt));
            DriveHero(hero, velocity);
            yield return null;
            yield return null;
            Assert.IsNotEmpty(_beats, "The patrol sees the hero.");
            _lastSeen = PlaneOf(hero);
            var hidden = heroAt + new Vector2(0f, 340f);
            ShipTestFactory.MoveKinematicShip(hero, GamePlane.PlanePointToWorld(hidden));
            DriveHero(hero, velocity);
            yield return null;
            var planar = GamePlane.WorldDirToPlane(hero.Body.linearVelocity);
            _headingSpeed = planar.magnitude;
            _heading = _headingSpeed > 0f ? planar / _headingSpeed : Vector2.zero;
        }

        private sealed class PlacedRock : IObstacleField
        {
            public Collider Collider;
            public float Radius;

            public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer)
            {
                if (!Collider || buffer == null || buffer.Length == 0) return 0;
                var plane = GamePlane.WorldPointToPlane(Collider.transform.position);
                if (Mathf.Abs(plane.x - centerPlane.x) > halfExtent || Mathf.Abs(plane.y - centerPlane.y) > halfExtent)
                    return 0;
                buffer[0] = new DetectedObstacle(Collider.transform.position, Radius, Collider);
                return 1;
            }
        }

        private GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private ExtractionZone NewZone(GameObject sector, string name)
        {
            var zone = Child(sector, name);
            var collider = zone.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 4f;
            return zone.AddComponent<ExtractionZone>();
        }

        private static IEnumerator WaitSeconds(Func<bool> done, float seconds)
        {
            var deadline = Time.time + seconds;
            while (!done() && Time.time < deadline)
                yield return null;
        }

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
        }
    }
}
#endif
