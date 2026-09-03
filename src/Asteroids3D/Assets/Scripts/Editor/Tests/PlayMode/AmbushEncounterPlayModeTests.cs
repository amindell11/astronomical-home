#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using Objectives;
using Ships;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>End-to-end ambush encounter: enter area → latched delay → token-gated wave spawn → concurrent local objective beside the live spine → clear → local closes and the cleared token latches. Plus two-instance concurrency and pending-fire cancellation.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class AmbushEncounterPlayModeTests : PlayModeWorldFixture
    {
        private const string AreaToken = "near-ambush";
        private const string StartToken = "ambush-started";
        private const string ClearedToken = "ambush-cleared";
        private static readonly Vector2 TriggerPlane = new(-10f, 25f);

        private class BusProbeSector : Sector
        {
            public SectorBuildContext Ctx => Context;
        }

        private UnitService _unitService;
        private GameServices _services;
        private ObjectiveService _objectives;
        private SectorSettings _config;
        private Ship _waveTemplate;
        private readonly List<GameObject> _created = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            var unitServiceGO = TrackGO(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();

            var objectiveServiceGO = TrackGO(new GameObject("ObjectiveService"));
            _objectives = objectiveServiceGO.AddComponent<ObjectiveService>();

            var arena = Tests.Common.TestArena.On(unitServiceGO, _unitService.Registry);
            _unitService.SetArena(arena);
            var projectiles = new ProjectileService(unitServiceGO.transform);
            _unitService.SetProjectiles(projectiles);
            _services = new GameServices(
                _unitService, projectiles, new EnvironmentService(), _objectives,
                new CameraService(), new UIService(), arena);

            _config = ScriptableObject.CreateInstance<SectorSettings>();
            // Primitive test ship, not Ship_2: its layer-7 collider needs LFS geometry.
            _waveTemplate = ShipTestFactory.CreateKinematicPrimitiveShipAt(new Vector2(1000f, 1000f));
            TrackGO(_waveTemplate.gameObject);
        }

        [TearDown]
        public override void TearDown()
        {
            _unitService?.Clear();

            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();

            if (_config != null) { Object.DestroyImmediate(_config); _config = null; }

            base.TearDown();
        }

        private GameObject TrackGO(GameObject go) { _created.Add(go); return go; }

        private Ship SpawnKinematicPlayer()
        {
            var ship = ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero);
            TrackGO(ship.gameObject);
            return ship;
        }

        private (GameObject encounterGO, AmbushEncounter encounter, RingSpawner wave, TriggerVolume volume)
            AddAmbush(Sector sector, string areaToken, string startToken, string clearedToken,
                      Vector2 plane, float fireDelay, int waveCount = 2)
        {
            var encounterGO = new GameObject("Ambush");
            encounterGO.transform.SetParent(sector.transform);
            encounterGO.transform.position = GamePlane.PlanePointToWorld(plane);

            var triggerGO = new GameObject("Ambush Trigger");
            triggerGO.transform.SetParent(encounterGO.transform, false);
            var col = triggerGO.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 5f;
            var volume = triggerGO.AddComponent<TriggerVolume>();
            volume.Configure(areaToken);

            var waveGO = new GameObject("Ambush Wave");
            waveGO.transform.SetParent(encounterGO.transform, false);
            var wave = waveGO.AddComponent<RingSpawner>();
            wave.Configure(_waveTemplate, null, waveCount, radius: 10f, team: 1);
            wave.Configure(startToken);

            var encounter = encounterGO.AddComponent<AmbushEncounter>();
            encounter.Configure(new[] { ActivationTerm.Signal(areaToken) }, new[] { startToken }, fireDelay);
            encounter.Bind(wave, new[] { clearedToken });

            return (encounterGO, encounter, wave, volume);
        }

        private BusProbeSector CreateSector(Ship player)
        {
            var go = TrackGO(new GameObject("AmbushSector"));
            var sector = go.AddComponent<BusProbeSector>();
            sector.Initialize(_services, _config, player);
            return sector;
        }

        private static IEnumerator WaitFrames(System.Func<bool> done, int maxFrames = 300)
        {
            for (var i = 0; i < maxFrames && !done(); i++)
                yield return null;
        }

        // Batch-mode frames are sub-millisecond — a fireDelaySeconds wait must be bounded by game time, not frames.
        private static IEnumerator WaitSeconds(System.Func<bool> done, float maxSeconds)
        {
            var deadline = Time.time + maxSeconds;
            while (Time.time < deadline && !done())
                yield return null;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator EnterAreaThenLeave_DelayedWaveSpawns_LocalRunsBesideSpine_ClearLatchesToken()
        {
            var player = SpawnKinematicPlayer();
            var sector = CreateSector(player);

            var keyGO = new GameObject("Key");
            keyGO.transform.SetParent(sector.transform);
            keyGO.transform.position = GamePlane.PlanePointToWorld(new Vector2(200f, 200f));
            keyGO.AddComponent<SphereCollider>().isTrigger = true;
            var key = keyGO.AddComponent<KeyPickup>();
            var zoneGO = new GameObject("Zone");
            zoneGO.transform.SetParent(sector.transform);
            zoneGO.AddComponent<SphereCollider>().isTrigger = true;
            var zone = zoneGO.AddComponent<ExtractionZone>();
            var spine = sector.gameObject.AddComponent<SectorSpineModule>();
            spine.Bind(key, zone);

            var (_, encounter, wave, volume) = AddAmbush(
                sector, AreaToken, StartToken, ClearedToken, TriggerPlane, fireDelay: 1f);
            sector.SetManifest(null, new SectorSpawner[] { wave },
                new SectorModule[] { spine, volume, encounter });

            yield return sector.Setup();
            Assert.AreEqual(0, wave.Spawned.Count, "A token-gated wave must stay dormant at Build.");
            Assert.AreEqual(0, _objectives.Locals.Count);

            ShipTestFactory.MoveKinematicShip(player, GamePlane.PlanePointToWorld(TriggerPlane));
            yield return WaitSeconds(() => sector.Ctx.Bus.Get(AreaToken), 5f);
            Assert.IsTrue(sector.Ctx.Bus.Get(AreaToken), "Entering the area must raise the level.");
            Assert.IsFalse(encounter.HasFired, "The delay must hold the fire sequence.");
            Assert.AreEqual(0, wave.Spawned.Count);

            // Leaving the area must not cancel — the predicate latched on first satisfaction.
            ShipTestFactory.MoveKinematicShip(
                player, GamePlane.PlanePointToWorld(new Vector2(100f, 0f)));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            yield return WaitSeconds(() => encounter.HasFired, 5f);
            Assert.IsTrue(encounter.HasFired, "The latched delay must fire after fireDelaySeconds.");
            Assert.IsTrue(sector.Ctx.Bus.Get(StartToken));
            Assert.AreEqual(2, wave.Spawned.Count, "The token latch must produce the wave.");
            foreach (var ship in wave.Spawned)
                Assert.IsTrue(ship && ship.gameObject.activeInHierarchy);

            Assert.AreEqual(1, _objectives.Locals.Count, "The encounter must open its local objective.");
            Assert.AreEqual(ObjectiveType.ClearHostiles, _objectives.Locals[0].Tracker.CurrentState);
            Assert.AreEqual(SectorSpineModule.StepExplore, _objectives.SpineStep,
                "The local must run beside the untouched spine — several things alive at once.");

            foreach (var ship in wave.Spawned)
                ship.gameObject.SetActive(false);
            yield return WaitFrames(() => _objectives.Locals.Count == 0);
            Assert.AreEqual(0, _objectives.Locals.Count, "Clearing the wave must close the local.");
            Assert.IsTrue(sector.Ctx.Bus.Get(ClearedToken), "Clearing must latch the cleared token.");
            Assert.AreEqual(SectorSpineModule.StepExplore, _objectives.SpineStep);

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TwoInstances_OpenConcurrentLocals_AndClearIndependently()
        {
            var player = SpawnKinematicPlayer();
            var sector = CreateSector(player);

            var (_, encA, waveA, _) = AddAmbush(
                sector, "near-a", "started-a", "cleared-a", new Vector2(-40f, 0f), fireDelay: 0f, waveCount: 1);
            var (_, encB, waveB, _) = AddAmbush(
                sector, "near-b", "started-b", "cleared-b", new Vector2(40f, 0f), fireDelay: 0f, waveCount: 1);
            sector.SetManifest(null, new SectorSpawner[] { waveA, waveB }, new SectorModule[] { encA, encB });

            yield return sector.Setup();

            sector.Ctx.Bus.Set("near-a", true);
            sector.Ctx.Bus.Set("near-b", true);
            Assert.AreEqual(1, waveA.Spawned.Count);
            Assert.AreEqual(1, waveB.Spawned.Count);
            Assert.AreEqual(2, _objectives.Locals.Count, "Two instances must hold two concurrent locals.");

            waveA.Spawned[0].gameObject.SetActive(false);
            yield return WaitFrames(() => _objectives.Locals.Count == 1);
            Assert.IsTrue(sector.Ctx.Bus.Get("cleared-a"), "Clearing wave A must latch only A's token.");
            Assert.IsFalse(sector.Ctx.Bus.Get("cleared-b"));
            Assert.AreEqual(1, _objectives.Locals.Count, "B's local must survive A's completion.");

            waveB.Spawned[0].gameObject.SetActive(false);
            yield return WaitFrames(() => _objectives.Locals.Count == 0);
            Assert.IsTrue(sector.Ctx.Bus.Get("cleared-b"));

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Teardown_CancelsPendingFire_NoSpawnNoLocal()
        {
            var player = SpawnKinematicPlayer();
            var sector = CreateSector(player);

            var (_, encounter, wave, volume) = AddAmbush(
                sector, AreaToken, StartToken, ClearedToken, TriggerPlane, fireDelay: 30f);
            sector.SetManifest(null, new SectorSpawner[] { wave },
                new SectorModule[] { volume, encounter });

            yield return sector.Setup();

            ShipTestFactory.MoveKinematicShip(player, GamePlane.PlanePointToWorld(TriggerPlane));
            yield return WaitSeconds(() => sector.Ctx.Bus.Get(AreaToken), 5f);
            Assert.IsTrue(sector.Ctx.Bus.Get(AreaToken));
            Assert.IsFalse(encounter.HasFired);

            yield return sector.Teardown();
            yield return null;

            Assert.IsFalse(encounter.HasFired, "Teardown must cancel the pending fire.");
            Assert.AreEqual(0, wave.Spawned.Count, "No wave may spawn after teardown.");
            Assert.AreEqual(0, _objectives.Locals.Count);
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MissingWaveSpawner_LogsError_AndStaysInert()
        {
            var player = SpawnKinematicPlayer();
            var sector = CreateSector(player);

            var (_, encounter, wave, _) = AddAmbush(
                sector, AreaToken, StartToken, ClearedToken, TriggerPlane, fireDelay: 0f);
            encounter.Bind(null);
            sector.SetManifest(null, new SectorSpawner[] { wave },
                new SectorModule[] { encounter });

            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("AmbushEncounter .*inert"));
            yield return sector.Setup();

            sector.Ctx.Bus.Set(AreaToken, true);
            Assert.IsFalse(encounter.HasFired, "An inert encounter must never fire.");
            Assert.AreEqual(0, _objectives.Locals.Count);

            yield return sector.Teardown();
        }
    }
}
#endif
