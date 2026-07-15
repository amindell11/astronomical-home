using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>SectorSpineModule against the objective service and sector bus: step ports, terminal mapping, teardown hygiene.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorSpineModuleEditModeTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject NewGO(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
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

        private static void Run(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }

        private sealed class SpineRig
        {
            public SectorSpineModule Module;
            public ObjectiveService Svc;
            public KeyPickup Key;
            public ExtractionZone Zone;
            public SectorEventBus Bus;
            public SectorBuildContext Ctx;
            public SignalPort Explore;
            public SignalPort KeyAcquired;
            public SignalPort ReadyToExtract;
            public SignalPort Completed;
            public SignalPort Failed;
        }

        private SpineRig BuildSpine()
        {
            var rig = new SpineRig();
            rig.Svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();

            var keyGO = NewGO("Key");
            keyGO.AddComponent<SphereCollider>().isTrigger = true;
            rig.Key = keyGO.AddComponent<KeyPickup>();

            var zoneGO = NewGO("Zone");
            zoneGO.AddComponent<SphereCollider>().isTrigger = true;
            rig.Zone = zoneGO.AddComponent<ExtractionZone>();

            var moduleGO = NewGO("Spine");
            rig.Explore = moduleGO.AddComponent<SignalPort>();
            rig.KeyAcquired = moduleGO.AddComponent<SignalPort>();
            rig.ReadyToExtract = moduleGO.AddComponent<SignalPort>();
            rig.Completed = moduleGO.AddComponent<SignalPort>();
            rig.Failed = moduleGO.AddComponent<SignalPort>();
            rig.Module = moduleGO.AddComponent<SectorSpineModule>();
            rig.Module.Bind(rig.Key, rig.Zone,
                rig.Explore, rig.KeyAcquired, rig.ReadyToExtract, rig.Completed, rig.Failed);

            rig.Bus = new SectorEventBus();
            rig.Ctx = new SectorBuildContext(new StubServices(rig.Svc), null, null, rig.Bus);
            return rig;
        }

        private static void InstallPostSpineMission(ObjectiveService svc)
        {
            svc.SetSpineObjective(
                new MissionDefinition(SectorSpineModule.StepExplore, new Dictionary<string, string>()),
                new Dictionary<string, Func<ObjectiveState>>
                {
                    [SectorSpineModule.StepExplore] = () => new CompletedState()
                });
        }

        [Test]
        public void Setup_InstallsSpine_AndLatchesInitialStepPortAndTarget()
        {
            var rig = BuildSpine();

            Run(rig.Module.Setup(rig.Ctx));

            Assert.AreEqual(SectorSpineModule.StepExplore, rig.Svc.SpineStep);
            Assert.IsTrue(rig.Bus.Get(rig.Explore), "The initial spine step must be latched on the bus.");
            Assert.AreEqual(rig.Key.transform, rig.Svc.SpineTarget,
                "The explore step must report the key as the spine target.");
        }

        [Test]
        public void Setup_WithUnassignedStepPort_LogsError_AndStaysInert()
        {
            var rig = BuildSpine();
            rig.Module.Bind(rig.Key, rig.Zone,
                rig.Explore, rig.KeyAcquired, null, rig.Completed, rig.Failed);

            LogAssert.Expect(LogType.Error,
                new Regex($"SectorSpineModule .*unassigned {SectorSpineModule.StepReadyToExtract} port.*inert"));
            Run(rig.Module.Setup(rig.Ctx));

            Assert.IsNull(rig.Svc.SpineTracker, "An inert spine must not install a mission.");
            Assert.IsFalse(rig.Bus.Get(rig.Explore));
        }

        [Test]
        public void SpineFail_LatchesFailedPort_AndEndsSectorFailed()
        {
            var rig = BuildSpine();
            Run(rig.Module.Setup(rig.Ctx));

            SectorResult? got = null;
            rig.Module.SectorEndRequested += r => got = r;

            rig.Svc.SpineTracker.Fail();
            rig.Svc.Tick(0.1f);

            Assert.IsTrue(got.HasValue, "The failed terminal state must end the sector.");
            Assert.IsFalse(got.Value.Success);
            Assert.AreEqual("spine_failed", got.Value.FailReason);
            Assert.IsTrue(rig.Bus.Get(rig.Failed));
        }

        [Test]
        public void UnknownSpineStep_LogsError_InsteadOfSilentlySkippingThePort()
        {
            var rig = BuildSpine();
            Run(rig.Module.Setup(rig.Ctx));

            LogAssert.Expect(LogType.Error,
                new Regex("SectorSpineModule .*no port for unknown spine step 'bogus'"));
            rig.Svc.SetSpineObjective(
                new MissionDefinition("bogus", new Dictionary<string, string>()),
                new Dictionary<string, Func<ObjectiveState>> { ["bogus"] = () => new CompletedState() });
        }

        [Test]
        public void Teardown_ClearsSpine_AndUnsubscribesFromStepEvents()
        {
            var rig = BuildSpine();
            Run(rig.Module.Setup(rig.Ctx));

            Run(rig.Module.Teardown(rig.Ctx));

            Assert.IsNull(rig.Svc.SpineTracker, "Teardown must clear the spine.");
            Assert.IsNull(rig.Svc.SpineTarget);

            InstallPostSpineMission(rig.Svc);
            Assert.IsNull(rig.Svc.SpineTarget,
                "A torn-down module must not react to later spine installs (leaked step subscription).");
        }

        [Test]
        public void SpineStepEvent_FiresOnInstall_AndOnEveryTransition()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var steps = new List<string>();
            svc.OnSpineStepChanged += steps.Add;

            svc.SetSpineObjective(
                new MissionDefinition("a", new Dictionary<string, string> { { "a", "b" } }),
                new Dictionary<string, Func<ObjectiveState>>
                {
                    // Both steps share ObjectiveType.Explore — the type-level event stays silent, the step event must not.
                    ["a"] = () => new AlwaysCompleteExploreState(),
                    ["b"] = () => new AlwaysCompleteExploreState()
                });
            Assert.AreEqual(new[] { "a" }, steps, "Install must publish the initial step.");

            var typeTransitions = 0;
            svc.OnSpineStateChanged += (_, _) => typeTransitions++;
            svc.Tick(0.1f);

            Assert.AreEqual(new[] { "a", "b" }, steps,
                "A same-type step transition must still raise the step event.");
            Assert.AreEqual(0, typeTransitions,
                "Sanity: the type-level event suppresses same-type transitions — this is why the step seam exists.");
        }

        private sealed class AlwaysCompleteExploreState : ObjectiveState
        {
            public override ObjectiveType StateType => ObjectiveType.Explore;
            public override void Tick(float deltaTime) { }
            public override bool IsComplete => true;
        }
    }
}
