#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using AI;
using Damage;
using NUnit.Framework;
using Objectives;
using Objectives.Presentation;
using Ships;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Sectors
{
    /// <summary>A sector's hunt with real patrols: unaware until one sees the hero (or is hit by it), then a call-in that sounds the alarm, wakes the patrols and opens phase one with the HUD reading the search and then the call-in; asteroids hide the hero, and killing the caller inside its call-in silences it, leaving the search clock to end the hunt.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorHuntPlayModeTests : PlayModeWorldFixture
    {
        private const string PatrolPilotPath = "Assets/Prefabs/Pilots/AggressorPilot.prefab";
        private const float SpawnDistance = 30f;
        private const int SectorIndex = 1;

        private UnitService _unitService;
        private ObjectiveService _objectives;
        private readonly List<Object> _created = new();
        private readonly List<string> _beats = new();

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
        public IEnumerator PatrolThatSeesTheHero_CallsItIn_ThenTheAlarmWakesItAndOpensPhaseOne()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 20f, callInSeconds: .5f, searchSeconds: 60f);
            var template = ShipTestFactory.CreateKinematicPrimitiveShipAt(new Vector2(500f, 500f));
            Track(template.gameObject).SetActive(false);
            var sectorGO = Track(new GameObject("HuntedSector"));
            hunt.transform.SetParent(sectorGO.transform, false);
            var director = Child(sectorGO, "WaveDirector").AddComponent<WaveDirector>();
            director.Configure(new[] { new WaveDirector.RosterEntry { template = template } },
                new[] { new SectorPhase { seconds = 30f, interval = 30f, cap = 0 } }, openingLullSeconds: 5f, hunt: hunt);
            var spine = sectorGO.AddComponent<PhaseSpineModule>();
            spine.Bind(director, NewZone(sectorGO, "Zone"), NewZone(sectorGO, "Beacon"), hunt: hunt);
            var sector = sectorGO.AddComponent<Sector>();
            sector.SetManifest(null, new SectorSpawner[] { director, hunt }, new SectorModule[] { spine });
            sector.Initialize(_unitService, _objectives, true, default, hero, SectorIndex);

            yield return sector.Setup();
            yield return null;
            yield return null;
            var patrol = Patrol(hunt);
            Assert.AreEqual(SpawnDistance, PlaneDistance(patrol, hero), 2f, "The patrol spawns its distance from the start.");
            Assert.IsTrue(Scout(patrol).Unaware, "A patrol roams unaware of the hero.");
            Assert.AreEqual(HuntReading.Searching, Progress().Hunt, "The countdown reads the search.");
            Assert.IsTrue(director.Phase.InOpeningLull);

            ShipTestFactory.MoveKinematicShip(hero, patrol.transform.position + GamePlane.Right * 10f);
            yield return null;
            yield return null;
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {patrol.name}" }, _beats);
            Assert.AreEqual(HuntReading.Spotted, Progress().Hunt, "The countdown reads the call-in.");
            Assert.That(Progress().SecondsLeft, Is.LessThanOrEqualTo(.5f));
            Assert.IsTrue(Scout(patrol).Unaware, "A calling patrol still holds fire.");

            yield return WaitSeconds(() => director.Phase.Index == 0 && !director.Phase.InLull, 3f);
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {patrol.name}", $"alarm {SectorIndex} Patrol {patrol.name}" },
                _beats);
            Assert.IsFalse(director.Phase.InLull, "Phase one opens at the alarm.");
            Assert.AreEqual(0, director.Phase.Index, "The hunt adds no sector phase.");
            Assert.IsFalse(Scout(patrol).Unaware, "The alarm wakes the patrol.");
            Assert.AreEqual(HuntReading.None, Progress().Hunt);

            yield return sector.Teardown();
            yield return null;
            Assert.IsFalse(patrol, "Teardown despawns the patrols.");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator KillingTheCallerInsideItsCallIn_SilencesIt_AndTheSearchClockEndsTheHunt()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 50f, callInSeconds: 1f, searchSeconds: 3f);
            yield return Build(hunt, hero);
            yield return null;
            yield return null;
            var patrol = Patrol(hunt);
            var name = patrol.name;
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {name}" }, _beats, "A patrol in sight starts calling.");

            patrol.Damage.TakeDamage(new DamageInfo(1e6f, DamageKind.Laser, hero.Id, 0f, Vector3.zero, patrol.transform.position));
            yield return null;
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {name}", $"silenced {SectorIndex} {name}" }, _beats);
            Assert.IsEmpty(hunt.Spawned, "A dead patrol is despawned.");

            yield return WaitSeconds(() => hunt.Alarmed, 5f);
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {name}", $"silenced {SectorIndex} {name}", $"alarm {SectorIndex} SearchClock none" },
                _beats, "The silenced call-in never sounds; the search clock does.");
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
            var hunt = NewHunt(sightRadius: 50f, callInSeconds: 30f, searchSeconds: 60f);
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
        public IEnumerator AHitFromTheHero_StartsTheCallIn_OutOfSight()
        {
            var hero = SpawnHero();
            var hunt = NewHunt(sightRadius: 5f, callInSeconds: 30f, searchSeconds: 60f);
            yield return Build(hunt, hero);
            yield return null;
            var patrol = Patrol(hunt);
            Assert.IsEmpty(_beats);

            patrol.Damage.TakeDamage(new DamageInfo(1f, DamageKind.Laser, hero.Id, 0f, Vector3.zero, patrol.transform.position));
            Assert.AreEqual(new[] { $"spotted {SectorIndex} {patrol.name}" }, _beats, "A patrol the hero hits has spotted it.");
        }

        private Ship SpawnHero() => Track(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();

        private SectorHunt NewHunt(float sightRadius, float callInSeconds, float searchSeconds)
        {
            var feel = Track(ScriptableObject.CreateInstance<HuntFeelProfile>());
            feel.sightRadius = sightRadius;
            feel.callInSeconds = callInSeconds;
            feel.searchSeconds = searchSeconds;
            var hunt = Track(new GameObject("Hunt")).AddComponent<SectorHunt>();
            hunt.Configure(feel, TestAssets.LoadCrimsonPrefab(), TestAssets.Load<AICommander>(PatrolPilotPath), 1,
                SpawnDistance);
            hunt.PatrolSpotted += (sector, patrol) => _beats.Add($"spotted {sector} {patrol.name}");
            hunt.PatrolSilenced += (sector, patrol) => _beats.Add($"silenced {sector} {patrol.name}");
            hunt.AlarmRaised += (sector, reason, caller) =>
                _beats.Add($"alarm {sector} {reason} {(caller ? caller.name : "none")}");
            return hunt;
        }

        private IEnumerator Build(SectorHunt hunt, Ship hero) =>
            hunt.Build(new SectorBuildContext(_unitService, _objectives, true, null, default, hero: hero,
                sectorIndex: SectorIndex));

        private static Ship Patrol(SectorHunt hunt)
        {
            Assert.AreEqual(1, hunt.Spawned.Count, "The hunt spawns its patrol on its first tick.");
            return hunt.Spawned[0];
        }

        private static Scout Scout(Ship patrol) => ((AICommander)patrol.Commander).Scout;

        private SectorPhaseProgress Progress() => _objectives.SpineTracker.PhaseProgress.Value;

        private static float PlaneDistance(Ship a, Ship b) => Vector2.Distance(
            GamePlane.WorldPointToPlane(a.transform.position), GamePlane.WorldPointToPlane(b.transform.position));

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
