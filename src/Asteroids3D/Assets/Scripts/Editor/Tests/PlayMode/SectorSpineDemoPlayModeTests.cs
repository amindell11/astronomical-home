#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// <summary>End-to-end demo spine: key pickup advances the spine over the bus, the extraction rule activates the chaser, and reaching the gate extracts — including parked-in-gate-then-get-key with no physics-frame hack.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorSpineDemoPlayModeTests : PlayModeWorldFixture
    {
        private static readonly Vector2 GatePlane = new(50f, 50f);

        private UnitService _unitService;
        private GameServices _services;
        private ObjectiveService _objectives;
        private SectorSettings _config;
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
            _services = new GameServices(
                _unitService, new ProjectileService(), new EnvironmentService(), _objectives,
                new CameraService(), new UIService(), arena);

            _config = ScriptableObject.CreateInstance<SectorSettings>();
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

        private Ship SpawnKinematicShip(Vector2 planePos)
        {
            var prefab = TestAssets.LoadShip2Prefab();
            if (!prefab) Assert.Ignore("Required test assets not found.");
            var ship = _unitService.SpawnShip(
                prefab, null, 0, GamePlane.PlanePointToWorld(planePos), GamePlane.Rotation);
            ship.Body.isKinematic = true;
            return ship;
        }

        private const string ChallengeToken = "extraction-challenge-started";

        private (Sector sector, KeyPickup key, ExtractionZone zone, Ship player, Ship chaser) BuildDemoSector(
            Vector2? chaserPlane = null, bool includeRule = true, bool bindZone = true,
            string activateToken = ChallengeToken)
        {
            var player = SpawnKinematicShip(Vector2.zero);
            var chaser = SpawnKinematicShip(chaserPlane ?? new Vector2(300f, 300f));
            chaser.gameObject.SetActive(false);
            var activate = chaser.gameObject.AddComponent<ActivateOnToken>();
            activate.Configure(activateToken);

            var sectorGO = TrackGO(new GameObject("SpineDemoSector"));
            var sector = sectorGO.AddComponent<Sector>();

            var keyGO = new GameObject("Key");
            keyGO.transform.SetParent(sectorGO.transform);
            keyGO.transform.position = GamePlane.PlanePointToWorld(new Vector2(-25f, 50f));
            var keyCol = keyGO.AddComponent<SphereCollider>();
            keyCol.isTrigger = true;
            keyCol.radius = 2f;
            var key = keyGO.AddComponent<KeyPickup>();

            var gateGO = new GameObject("Gate");
            gateGO.transform.SetParent(sectorGO.transform);
            gateGO.transform.position = GamePlane.PlanePointToWorld(GatePlane);
            var gateCol = gateGO.AddComponent<SphereCollider>();
            gateCol.isTrigger = true;
            gateCol.radius = 4f;
            var zone = gateGO.AddComponent<ExtractionZone>();
            zone.Bind(chaser.transform);
            var volume = gateGO.AddComponent<TriggerVolume>();
            volume.Configure("in-gate");

            var spine = sectorGO.AddComponent<SectorSpineModule>();
            spine.Bind(key, zone);

            var modules = new List<SectorModule> { spine, volume };
            if (includeRule)
            {
                var rule = gateGO.AddComponent<ExtractionChallengeRule>();
                rule.Configure(
                    new[] { ActivationTerm.Signal(SectorSpineModule.TokenPrefix + SectorSpineModule.StepReadyToExtract) },
                    new[] { ChallengeToken });
                rule.Bind(bindZone ? zone : null);
                modules.Add(rule);
            }
            modules.Add(activate);

            sector.SetManifest(null, null, modules.ToArray());
            sector.Initialize(_services, _config, player);
            return (sector, key, zone, player, chaser);
        }

        private static IEnumerator WaitFrames(System.Func<bool> done, int maxFrames = 120)
        {
            for (var i = 0; i < maxFrames && !done(); i++)
                yield return null;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator KeyThenGate_AdvancesSpine_ActivatesChaser_AndExtracts()
        {
            var (sector, key, zone, player, chaser) = BuildDemoSector();
            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();

            Assert.AreEqual(SectorSpineModule.StepExplore, _objectives.SpineStep);
            Assert.AreEqual(key.transform, _objectives.SpineTarget);
            Assert.IsFalse(chaser.gameObject.activeSelf, "The chaser must stay dormant until the rule fires.");

            player.transform.position = key.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(key.PlayerHasKey, "Flying into the key must collect it.");

            yield return WaitFrames(() => _objectives.SpineStep == SectorSpineModule.StepReadyToExtract);
            Assert.AreEqual(SectorSpineModule.StepReadyToExtract, _objectives.SpineStep);
            Assert.IsTrue(chaser.gameObject.activeSelf,
                "Reaching ready-to-extract must fire the extraction rule and activate the chaser via the bus token.");
            Assert.AreEqual(zone.transform, _objectives.SpineTarget,
                "The spine target must move to the gate for the extraction step.");

            player.transform.position = GamePlane.PlanePointToWorld(GatePlane);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => got.HasValue);

            Assert.IsTrue(got.HasValue, "Reaching the gate with the key must end the sector.");
            Assert.IsTrue(got.Value.Success, "The completed spine must end the sector as Extracted.");

            yield return sector.Teardown();
            Assert.IsFalse(chaser.gameObject.activeSelf, "Teardown must restore the chaser dormant.");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ParkedInGate_ThenKeyArrives_StillExtracts()
        {
            var (sector, key, _, player, chaser) = BuildDemoSector();
            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();

            player.transform.position = GamePlane.PlanePointToWorld(GatePlane);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(SectorSpineModule.StepExplore, _objectives.SpineStep,
                "Parking in the inert gate before the key must not advance anything.");
            Assert.IsFalse(chaser.gameObject.activeSelf);

            // The key comes to the parked player — the player never exits and re-enters the gate volume.
            key.transform.position = player.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(key.PlayerHasKey);

            yield return WaitFrames(() => got.HasValue);

            Assert.IsTrue(got.HasValue,
                "A player parked in the gate before qualifying must still extract — occupancy is a level, not an enter-edge.");
            Assert.IsTrue(got.Value.Success);
            Assert.IsTrue(chaser.gameObject.activeSelf, "The rule must still have fired on the way through ready-to-extract.");

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ChaserWithinBlockDistance_BlocksExtraction_UntilChaserLeaves()
        {
            var (sector, key, _, player, chaser) = BuildDemoSector(chaserPlane: new Vector2(55f, 50f));
            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();

            player.transform.position = key.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => _objectives.SpineStep == SectorSpineModule.StepReadyToExtract);
            Assert.IsTrue(chaser.gameObject.activeSelf, "The rule must have activated the chaser.");

            player.transform.position = GamePlane.PlanePointToWorld(GatePlane);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => got.HasValue, maxFrames: 30);
            Assert.IsFalse(got.HasValue,
                "A chaser within blockDistance must block extraction even with the player parked in the gate.");

            chaser.transform.position = GamePlane.PlanePointToWorld(new Vector2(300f, 300f));
            yield return WaitFrames(() => got.HasValue);
            Assert.IsTrue(got.HasValue, "Moving the chaser out of blockDistance must unblock extraction.");
            Assert.IsTrue(got.Value.Success, "The unblocked extraction must end the sector as Extracted.");

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator MissingChallengeRule_ZoneStaysUnarmed_AndExtractionNeverCompletes()
        {
            var (sector, key, _, player, _) = BuildDemoSector(includeRule: false);
            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();

            player.transform.position = key.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => _objectives.SpineStep == SectorSpineModule.StepReadyToExtract);

            player.transform.position = GamePlane.PlanePointToWorld(GatePlane);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => got.HasValue, maxFrames: 30);

            Assert.IsFalse(got.HasValue,
                "With no challenge rule the zone must stay unarmed — extraction must not complete silently.");
            Assert.AreEqual(SectorSpineModule.StepReadyToExtract, _objectives.SpineStep);

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator RuleWithNullZone_LogsError_AndExtractionStaysGated()
        {
            var (sector, key, _, player, chaser) = BuildDemoSector(bindZone: false);
            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            LogAssert.Expect(LogType.Error,
                new Regex("ExtractionChallengeRule .*missing a fixture reference.*inert"));
            yield return sector.Setup();

            player.transform.position = key.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => _objectives.SpineStep == SectorSpineModule.StepReadyToExtract);

            player.transform.position = GamePlane.PlanePointToWorld(GatePlane);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => got.HasValue, maxFrames: 30);

            Assert.IsFalse(got.HasValue,
                "An inert rule (null zone) must leave the zone unarmed — extraction must not complete.");
            Assert.IsFalse(chaser.gameObject.activeSelf,
                "An inert rule never publishes, so the chaser must stay dormant.");

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ActivateWithBlankToken_LogsError_AndChaserStaysDormant()
        {
            var (sector, key, _, player, chaser) = BuildDemoSector(activateToken: "");
            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            LogAssert.Expect(LogType.Error, new Regex("ActivateOnToken .*blank token.*inert"));
            yield return sector.Setup();

            player.transform.position = key.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => _objectives.SpineStep == SectorSpineModule.StepReadyToExtract);

            player.transform.position = GamePlane.PlanePointToWorld(GatePlane);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitFrames(() => got.HasValue);

            Assert.IsTrue(got.HasValue,
                "The rule and zone are wired — extraction must still complete past a broken activation listener.");
            Assert.IsFalse(chaser.gameObject.activeSelf,
                "An inert listener must fail closed: the chaser never activates.");

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator SpineModule_DestroyedWithoutTeardown_StopsReceivingStepEvents()
        {
            var svcGO = TrackGO(new GameObject("LocalObjectiveService"));
            var svc = svcGO.AddComponent<ObjectiveService>();

            var keyGO = TrackGO(new GameObject("Key"));
            keyGO.AddComponent<SphereCollider>().isTrigger = true;
            var key = keyGO.AddComponent<KeyPickup>();

            var zoneGO = TrackGO(new GameObject("Zone"));
            zoneGO.AddComponent<SphereCollider>().isTrigger = true;
            var zone = zoneGO.AddComponent<ExtractionZone>();

            var moduleGO = TrackGO(new GameObject("Spine"));
            var module = moduleGO.AddComponent<SectorSpineModule>();
            module.Bind(key, zone);

            var ctx = new SectorBuildContext(new StubServices(svc), null, null, new SectorEventBus());
            yield return module.Setup(ctx);
            Assert.AreEqual(key.transform, svc.SpineTarget, "Sanity: the live module reports the spine target.");

            Object.Destroy(moduleGO);
            yield return null;

            svc.SetSpineObjective(
                new MissionDefinition(SectorSpineModule.StepExplore, new Dictionary<string, string>()),
                new Dictionary<string, System.Func<ObjectiveState>>
                {
                    [SectorSpineModule.StepExplore] = () => new Objectives.States.CompletedState()
                });

            Assert.IsNull(svc.SpineTarget,
                "A module destroyed without Teardown must not react to later spine installs (leaked step subscription).");
        }

        private sealed class StubServices : IGameServices
        {
            private readonly IObjectiveService objectives;
            public StubServices(IObjectiveService objectives) => this.objectives = objectives;
            public IUnitService UnitService => null;
            public IEnvironmentService EnvironmentService => null;
            public IObjectiveService ObjectiveService => objectives;
            public ICameraService CameraService => null;
            public IUIService UIService => null;
            public ArenaContext Arena => null;
        }
    }
}
#endif
