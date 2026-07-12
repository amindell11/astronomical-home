#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using AI;
using Game;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the Attempt-3 sector composition system (serialized manifest +
    /// deterministic injection — no runtime crawl). Sectors are constructed programmatically:
    /// content children are authored under the sector, the manifest (<c>adopted[]</c> /
    /// <c>spawners[]</c>) is populated directly (mirroring what the editor Sync bakes), then
    /// <c>Setup()</c> adopts/builds from that manifest.
    /// </summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorCompositionPlayModeTests : PlayModeWorldFixture
    {
        private class TestSector : Sector
        {
            public bool IsBuilt => IsSetUp;
            protected override IEnumerator OnAfterTeardown()
            {
                Services.UnitService.Clear();
                yield break;
            }
        }

        private class TeardownStubSpawner : SectorSpawner
        {
            public bool ToreDown;
            public override IEnumerator Build(SectorBuildContext ctx) { yield break; }
            public override IEnumerator Teardown(SectorBuildContext ctx) { ToreDown = true; yield break; }
        }

        private class ProbeModule : SectorModule
        {
            public List<string> Log;
            public string Id;
            public override IEnumerator Setup(SectorBuildContext ctx) { Log.Add($"setup:{Id}"); yield break; }
            public override IEnumerator Teardown(SectorBuildContext ctx) { Log.Add($"teardown:{Id}"); yield break; }
        }

        private class EndModule : SectorModule
        {
            public void Fire(SectorResult r) => RequestSectorEnd(r);
        }

        /// <summary>Sector whose player is injected via Initialize (for EncounterSequenceModule's ctx.Player).</summary>
        private class EncounterTestSector : Sector
        {
            public bool IsBuilt => IsSetUp;
            protected override IEnumerator OnAfterTeardown() { Services.UnitService.Clear(); yield break; }
        }

        private class StubEncounter : Game.Encounters.Encounter
        {
            public bool OnFailCalled;
            public override Transform ObjectiveTarget => null;
            protected override IEnumerator OnSetup() { yield break; }
            protected override IEnumerator OnTeardown() { yield break; }
            protected override void OnFail() => OnFailCalled = true;
            public void Complete() => CompleteEncounter(Game.Encounters.EncounterResult.Completed);
            public void FailNow() => CompleteEncounter(Game.Encounters.EncounterResult.Failed);
        }

        private UnitService _unitService;
        private GameServices _services;
        private SectorSettings _config;
        private readonly List<GameObject> _created = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            var unitServiceGO = TrackGO(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();

            var objectiveServiceGO = TrackGO(new GameObject("ObjectiveService"));
            var objectiveService = objectiveServiceGO.AddComponent<ObjectiveService>();

            var arena = Tests.Common.TestArena.On(unitServiceGO, _unitService.Registry);
            _unitService.SetArena(arena);
            _services = new GameServices(
                _unitService, new EnvironmentService(), objectiveService,
                new CameraService(), new UIService(), arena);

            _config = ScriptableObject.CreateInstance<SectorSettings>();
        }

        [TearDown]
        public override void TearDown()
        {
            _services?.CameraService?.Clear();
            _unitService?.Clear();

            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();

            if (_config != null) { Object.DestroyImmediate(_config); _config = null; }

            base.TearDown();
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private GameObject TrackGO(GameObject go) { _created.Add(go); return go; }

        private TestSector CreateTestSector()
        {
            var go = TrackGO(new GameObject("TestSector"));
            // Author content under an INACTIVE sector — mirrors SessionHost's inactive holder
            // so authored ship children do not Awake/FixedUpdate before adoption initialises them.
            go.SetActive(false);
            var sector = go.AddComponent<TestSector>();
            sector.Initialize(_services, _config, null);
            return sector;
        }

        // A bare base Sector that does NOT force-Clear on teardown, so tests can verify the
        // per-producer despawn path itself clears sector-owned ships (the restart-leak fix).
        private Sector CreateBareSector()
        {
            var go = TrackGO(new GameObject("BareSector"));
            go.SetActive(false);
            var sector = go.AddComponent<Sector>();
            sector.Initialize(_services, _config, null);
            return sector;
        }

        private static void SetManifest(Sector sector, AdoptEntry[] adopted, SectorSpawner[] spawners)
            => sector.SetManifest(adopted, spawners, null);

        private Ship AddAdoptedShipChild(
            Transform parent, Ship shipPrefab, Commander commanderPrefab,
            int team = 0, Vector3? localPos = null)
        {
            var ship = Object.Instantiate(shipPrefab, parent);
            if (localPos.HasValue) ship.transform.localPosition = localPos.Value;
            ship.teamNumber = team;
            if (commanderPrefab)
                Object.Instantiate(commanderPrefab, ship.transform); // pilot authored as a child
            return ship;
        }

        private ProbeModule AddProbeModule(Sector sector, string id, List<string> log)
        {
            var go = TrackGO(new GameObject($"Module_{id}"));
            go.transform.SetParent(sector.transform);
            var m = go.AddComponent<ProbeModule>();
            m.Log = log;
            m.Id = id;
            return m;
        }

        private static AdoptEntry Entry(Component target, int team = 0, bool startActive = true) =>
            new AdoptEntry { target = target, team = team, startActive = startActive };

        // ── Tests ───────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Adopt_AllShipEntries_RegisteredAtPoses()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var sector = CreateTestSector();
            var positions = new[] { new Vector3(5, 0, 0), new Vector3(-5, 0, 0), new Vector3(0, 0, 5) };
            var entries = new List<AdoptEntry>();
            foreach (var pos in positions)
                entries.Add(Entry(AddAdoptedShipChild(sector.transform, ship, cmdr, localPos: pos)));
            SetManifest(sector, entries.ToArray(), null);

            yield return sector.Setup();

            Assert.AreEqual(3, _unitService.ActiveRegistry.ActiveShips.Count,
                "Each adopt entry must register exactly one ship.");
            foreach (var s in _unitService.ActiveRegistry.ActiveShips)
            {
                var near = System.Array.Exists(positions, p => Vector3.Distance(s.transform.position, p) < 0.5f);
                Assert.IsTrue(near, $"Adopted ship at {s.transform.position} is not near any expected pose.");
            }
        }

        [UnityTest]
        public IEnumerator Adopt_AppliesTeamAnnotation()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var sector = CreateTestSector();
            var child = AddAdoptedShipChild(sector.transform, ship, cmdr, team: 0);
            const int annotatedTeam = 2;
            SetManifest(sector, new[] { Entry(child, team: annotatedTeam) }, null);

            yield return sector.Setup();

            Assert.AreEqual(annotatedTeam, child.teamNumber,
                "Adopt entry's team annotation must be applied to the adopted ship.");
            Assert.IsTrue(_unitService.ActiveRegistry.ActiveShips.Contains(child));
        }

        [UnityTest]
        public IEnumerator Adopt_StartActiveFalse_StaysInactive()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var sector = CreateTestSector();
            var child = AddAdoptedShipChild(sector.transform, ship, cmdr);
            SetManifest(sector, new[] { Entry(child, startActive: false) }, null);

            yield return sector.Setup();

            Assert.IsFalse(child.gameObject.activeSelf,
                "An adopt entry with startActive=false must leave the ship inactive.");
        }

        [UnityTest]
        public IEnumerator Adopt_NestedPilotOverride_Survives()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var sector = CreateTestSector();
            var child = AddAdoptedShipChild(sector.transform, ship, cmdr);
            const float expected = 0.123f;
            var authoredPilot = child.GetComponentInChildren<AICommander>(true);
            Assert.IsNotNull(authoredPilot, "Authored ship must contain a pilot child.");
            authoredPilot.difficulty = expected;
            SetManifest(sector, new[] { Entry(child) }, null);

            yield return sector.Setup();

            var adopted = child.Commander as AICommander;
            Assert.IsNotNull(adopted, "Adopted ship must have an AICommander.");
            Assert.AreEqual(expected, adopted.difficulty, 0.001f,
                "The override on the authored pilot child must survive adoption (the placed object IS the runtime object).");
        }

        [UnityTest]
        public IEnumerator Spawn_RingSpawner_PopulatesSpawned()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var sector = CreateTestSector();
            var spawnerGO = new GameObject("Ring");
            spawnerGO.transform.SetParent(sector.transform);
            var ring = spawnerGO.AddComponent<RingSpawner>();
            ring.Configure(ship, cmdr, count: 3, radius: 20f);
            SetManifest(sector, null, new SectorSpawner[] { ring });

            yield return sector.Setup();

            Assert.AreEqual(3, ring.Spawned.Count, "RingSpawner must populate Spawned with 'count' ships.");
            Assert.AreEqual(3, _unitService.ActiveRegistry.ActiveShips.Count,
                "Ring ships must be registered with the unit service.");
            foreach (var s in ring.Spawned)
                Assert.Less(Mathf.Abs(GamePlane.WorldPointToPlane(s.transform.position).magnitude - 20f), 1f,
                    "Ring ships must sit on the configured radius.");
        }

        [UnityTest]
        public IEnumerator Teardown_InvokesSpawnerTeardown_AndClearsShips()
        {
            var sector = CreateTestSector();
            var stubGO = new GameObject("TeardownStub");
            stubGO.transform.SetParent(sector.transform);
            var stub = stubGO.AddComponent<TeardownStubSpawner>();
            SetManifest(sector, null, new SectorSpawner[] { stub });

            yield return sector.Setup();
            Assert.IsTrue(sector.IsBuilt);
            Assert.IsFalse(stub.ToreDown);

            yield return sector.Teardown();

            Assert.IsTrue(stub.ToreDown, "Spawner.Teardown must run during sector Teardown.");
            Assert.IsFalse(sector.IsBuilt, "IsSetUp must be false after Teardown.");
            Assert.AreEqual(0, _unitService.ActiveRegistry.ActiveShips.Count,
                "Adopted/spawned ships must be cleared on teardown.");
        }

        [UnityTest]
        public IEnumerator Teardown_DespawnsSpawnerProducts_WithoutForceClear()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var sector = CreateBareSector();
            var spawnerGO = new GameObject("Ring");
            spawnerGO.transform.SetParent(sector.transform);
            var ring = spawnerGO.AddComponent<RingSpawner>();
            ring.Configure(ship, cmdr, count: 3);
            SetManifest(sector, null, new SectorSpawner[] { ring });

            yield return sector.Setup();
            Assert.AreEqual(3, _unitService.ActiveRegistry.ActiveShips.Count, "Ring must spawn 3 ships.");

            yield return sector.Teardown();

            Assert.AreEqual(0, _unitService.ActiveRegistry.ActiveShips.Count,
                "The spawner's own Teardown must despawn its products (no force-Clear) so NPCs don't leak across restarts.");
            Assert.AreEqual(0, ring.Spawned.Count, "Spawned must be emptied after teardown.");
        }

        [UnityTest]
        public IEnumerator Teardown_DespawnsAdoptedShips_WithoutForceClear()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var sector = CreateBareSector();
            var child = AddAdoptedShipChild(sector.transform, ship, cmdr);
            SetManifest(sector, new[] { Entry(child) }, null);

            yield return sector.Setup();
            Assert.AreEqual(1, _unitService.ActiveRegistry.ActiveShips.Count, "Adopt must register 1 ship.");

            yield return sector.Teardown();

            Assert.AreEqual(0, _unitService.ActiveRegistry.ActiveShips.Count,
                "Adopted ships (root-detached, service-owned) must be despawned on teardown so they don't leak across restarts.");
        }

        // ── Module infrastructure (Stage 2.1) ────────────────────────────────────

        [UnityTest]
        public IEnumerator Modules_SetupInListOrder_TeardownReverseOrder()
        {
            var sector = CreateTestSector();
            var log = new List<string>();
            var a = AddProbeModule(sector, "A", log);
            var b = AddProbeModule(sector, "B", log);
            sector.SetManifest(null, null, new SectorModule[] { a, b });

            yield return sector.Setup();
            yield return sector.Teardown();

            CollectionAssert.AreEqual(
                new[] { "setup:A", "setup:B", "teardown:B", "teardown:A" }, log,
                "Modules must set up in list order and tear down in reverse.");
        }

        [UnityTest]
        public IEnumerator Module_RequestSectorEnd_AutoWiredToSectorComplete_AndUnsubscribedOnTeardown()
        {
            var sector = CreateTestSector();
            var endGO = TrackGO(new GameObject("EndModule"));
            endGO.transform.SetParent(sector.transform);
            var end = endGO.AddComponent<EndModule>();
            sector.SetManifest(null, null, new SectorModule[] { end });

            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();

            end.Fire(SectorResult.Failed("boom"));
            Assert.IsTrue(got.HasValue, "Module's RequestSectorEnd must reach the sector's completion sink.");
            Assert.IsFalse(got.Value.Success);

            yield return sector.Teardown();
            got = null;
            end.Fire(SectorResult.Extracted());
            Assert.IsFalse(got.HasValue, "Module end signal must be unsubscribed after teardown.");
        }

        // ── RespawnPolicy (Stage 2.4) — full death→revive pipeline with real ships ───

        [UnityTest]
        public IEnumerator Respawn_FixedPoint_RevivesAtPointAfterDeath()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var s = _unitService.SpawnShip(ship, cmdr, 0,
                GamePlane.PlanePointToWorld(Vector2.zero), GamePlane.Rotation);
            Assert.IsNotNull(s);

            var point = new Vector2(17f, -9f);
            var policy = new RespawnPolicy { origin = RespawnPolicy.Origin.FixedPoint, point = point, radius = 0f, delay = 0f };
            Assert.IsTrue(Respawn.Wire(s, policy, _services), "FixedPoint policy must wire a respawn.");

            TestDamage.Kill(s);

            // UnitService.Update processes the (delay 0) pending respawn next frame.
            yield return null;
            yield return null;

            var expected = GamePlane.PlanePointToWorld(point);
            Assert.Less(Vector3.Distance(s.transform.position, expected), 0.5f,
                $"Ship must revive at the FixedPoint pose {expected}, was {s.transform.position}.");
        }

        [UnityTest]
        public IEnumerator Respawn_OriginNone_DoesNotRevive()
        {
            var ship = TestAssets.LoadShip2Prefab();
            var cmdr = TestAssets.LoadTestPilotMpc();
            if (!ship || !cmdr) { Assert.Ignore("Required test assets not found."); yield break; }

            var spawnWorld = GamePlane.PlanePointToWorld(new Vector2(5f, 0f));
            var s = _unitService.SpawnShip(ship, cmdr, 0, spawnWorld, GamePlane.Rotation);
            Assert.IsNotNull(s);

            var policy = new RespawnPolicy { origin = RespawnPolicy.Origin.None };
            Assert.IsFalse(Respawn.Wire(s, policy, _services), "A None policy must wire nothing.");

            TestDamage.Kill(s);

            yield return null;
            yield return null;

            // No respawn was scheduled → the ship is not repositioned to any revive pose.
            Assert.Less(Vector3.Distance(s.transform.position, spawnWorld), 0.5f,
                "With Origin.None the ship must not be teleported by a respawn.");
        }

        // ── EncounterSequenceModule (Stage 2.5) ──────────────────────────────────

        // Active sector (coroutines started inside the module require an active GameObject).
        private EncounterTestSector CreateActiveEncounterSector(Ship player = null)
        {
            var go = TrackGO(new GameObject("EncounterSector"));
            var sector = go.AddComponent<EncounterTestSector>();
            sector.Initialize(_services, _config, player);
            return sector;
        }

        private StubEncounter NewStubEncounterTemplate(string name)
        {
            var go = TrackGO(new GameObject(name));
            return go.AddComponent<StubEncounter>();
        }

        private EncounterSequenceModule AttachEncounterModule(
            EncounterTestSector sector, params Game.Encounters.Encounter[] templates)
        {
            var module = sector.gameObject.AddComponent<EncounterSequenceModule>();
            module.SetEncounters(templates);
            sector.SetManifest(null, null, new SectorModule[] { module });
            return module;
        }

        [UnityTest]
        public IEnumerator EncounterModule_SequenceFinish_RaisesExtracted()
        {
            var sector = CreateActiveEncounterSector();
            var module = AttachEncounterModule(sector, NewStubEncounterTemplate("Enc0"));

            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();

            var active = module.Active as StubEncounter;
            Assert.IsNotNull(active, "Module must instantiate and start the first encounter.");

            active.Complete();
            for (var i = 0; i < 8 && !got.HasValue; i++) yield return null;

            Assert.IsTrue(got.HasValue, "Finishing the sequence must end the sector.");
            Assert.IsTrue(got.Value.Success, "A finished sequence ends the sector as Extracted.");

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator EncounterModule_EncounterFail_RaisesFailed()
        {
            var sector = CreateActiveEncounterSector();
            var module = AttachEncounterModule(sector, NewStubEncounterTemplate("Enc0"));

            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();

            var active = module.Active as StubEncounter;
            Assert.IsNotNull(active);

            active.FailNow();
            for (var i = 0; i < 4 && !got.HasValue; i++) yield return null;

            Assert.IsTrue(got.HasValue, "A failed encounter must end the sector.");
            Assert.IsFalse(got.Value.Success);
            Assert.AreEqual("encounter_failed", got.Value.FailReason);

            yield return sector.Teardown();
        }

        // NOTE: player-death → sector-restart is no longer the EncounterSequenceModule's job. That
        // responsibility now lives in the game driver (GameDriver.deathBehavior = RestartSector
        // injects GameSession.OnPlayerDeath, wired onto Ship.OnDeath by the rig). The former
        // EncounterModule_PlayerDeath_FailsActiveEncounter_AndRaisesFailed test was removed because the
        // module intentionally no longer subscribes to player death.
    }
}
#endif
