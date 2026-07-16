#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>PlayMode tests for the activation substrate across a real Sector lifecycle: fresh unfrozen bus per Setup, freeze-before-module-teardown, and no rule firing mid-teardown.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorActivationLifecyclePlayModeTests : PlayModeWorldFixture
    {
        private class BusProbeSector : Sector
        {
            public SectorBuildContext Ctx => Context;
        }

        private class FreezeProbeModule : SectorModule
        {
            public bool? BusFrozenAtTeardown;
            public override IEnumerator Teardown(SectorBuildContext ctx)
            {
                BusFrozenAtTeardown = ctx.Bus?.Frozen;
                yield break;
            }
        }

        private class SlowTeardownModule : SectorModule
        {
            public override IEnumerator Teardown(SectorBuildContext ctx)
            {
                yield return new WaitForSeconds(0.3f);
            }
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
            var projectiles = new ProjectileService(unitServiceGO.transform);
            _unitService.SetProjectiles(projectiles);
            _services = new GameServices(
                _unitService, projectiles, new EnvironmentService(), objectiveService,
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

        private GameObject TrackGO(GameObject go)
        {
            _created.Add(go);
            return go;
        }

        private BusProbeSector CreateSector()
        {
            var go = TrackGO(new GameObject("LifecycleSector"));
            var sector = go.AddComponent<BusProbeSector>();
            sector.Initialize(_services, _config, null);
            return sector;
        }

        private T AddModule<T>(Sector sector, string name) where T : SectorModule
        {
            var go = TrackGO(new GameObject(name));
            go.transform.SetParent(sector.transform);
            return go.AddComponent<T>();
        }

        [UnityTest]
        public IEnumerator SetupTeardownSetup_FreshUnfrozenBus_ClearedLatches_RuleRearmsAndFiresOnce()
        {
            var sector = CreateSector();
            var rule = AddModule<ActivationRule>(sector, "ChainRule");
            rule.Configure(new[] { ActivationTerm.Signal("go") }, new[] { "done" });
            sector.SetManifest(null, null, new SectorModule[] { rule });

            var fired = 0;
            rule.Fired += () => fired++;

            yield return sector.Setup();
            var bus1 = sector.Ctx.Bus;
            Assert.IsNotNull(bus1);
            Assert.IsFalse(bus1.Frozen);

            bus1.Set("go", true);
            Assert.AreEqual(1, fired);
            Assert.IsTrue(bus1.Get("done"));

            yield return sector.Teardown();
            Assert.IsTrue(bus1.Frozen, "Teardown must freeze the cycle's bus.");

            yield return sector.Setup();
            var bus2 = sector.Ctx.Bus;
            Assert.AreNotSame(bus1, bus2, "Each Setup must create a fresh bus.");
            Assert.IsFalse(bus2.Frozen, "The new cycle's bus must not inherit the frozen state.");
            Assert.IsFalse(bus2.Get("done"), "Latched tokens must not leak across cycles.");
            Assert.IsFalse(rule.HasFired, "The rule must re-arm on the new cycle.");

            bus2.Set("go", true);
            Assert.AreEqual(2, fired, "The rule must fire exactly once per cycle — no stale or duplicate subscriptions.");
            Assert.IsTrue(bus2.Get("done"));

            yield return sector.Teardown();
        }

        [UnityTest]
        public IEnumerator TimeRule_CrossingThresholdMidTeardown_NeverFires()
        {
            var sector = CreateSector();
            var rule = AddModule<ActivationRule>(sector, "TimeRule");
            rule.Configure(new[] { ActivationTerm.Time(0.1f) });
            var slow = AddModule<SlowTeardownModule>(sector, "SlowTeardown");
            var probe = AddModule<FreezeProbeModule>(sector, "FreezeProbe");
            // Reverse teardown order: probe and slow dismantle FIRST, leaving the rule live while time passes.
            sector.SetManifest(null, null, new SectorModule[] { rule, slow, probe });

            var fired = 0;
            rule.Fired += () => fired++;

            yield return sector.Setup();
            Assert.IsFalse(rule.HasFired);

            yield return sector.Teardown();

            Assert.AreEqual(true, probe.BusFrozenAtTeardown, "The bus must already be frozen when module teardown begins.");
            Assert.AreEqual(0, fired, "A time rule crossing its threshold mid-teardown must not fire on the frozen bus.");
            Assert.IsFalse(rule.HasFired);
        }
    }
}
#endif
