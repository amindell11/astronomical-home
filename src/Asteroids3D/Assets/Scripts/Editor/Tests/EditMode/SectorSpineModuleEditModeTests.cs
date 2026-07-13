using System;
using System.Collections;
using System.Collections.Generic;
using Game.Sectors;
using Game.Services;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>SectorSpineModule against the objective service and sector bus: step tokens, terminal mapping, teardown hygiene.</summary>
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

        private (SectorSpineModule module, ObjectiveService svc, KeyPickup key, ExtractionZone zone, SectorEventBus bus, SectorBuildContext ctx)
            BuildSpine()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();

            var keyGO = NewGO("Key");
            keyGO.AddComponent<SphereCollider>().isTrigger = true;
            var key = keyGO.AddComponent<KeyPickup>();

            var zoneGO = NewGO("Zone");
            zoneGO.AddComponent<SphereCollider>().isTrigger = true;
            var zone = zoneGO.AddComponent<ExtractionZone>();

            var module = NewGO("Spine").AddComponent<SectorSpineModule>();
            module.Bind(key, zone);

            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(new StubServices(svc), null, null, bus);
            return (module, svc, key, zone, bus, ctx);
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
        public void Setup_InstallsSpine_AndPublishesInitialStepTokenAndTarget()
        {
            var (module, svc, key, _, bus, ctx) = BuildSpine();

            Run(module.Setup(ctx));

            Assert.AreEqual(SectorSpineModule.StepExplore, svc.SpineStep);
            Assert.IsTrue(bus.Get(SectorSpineModule.TokenPrefix + SectorSpineModule.StepExplore),
                "The initial spine step must be latched on the bus.");
            Assert.AreEqual(key.transform, svc.SpineTarget,
                "The explore step must report the key as the spine target.");
        }

        [Test]
        public void FailSpine_LatchesFailedToken_AndEndsSectorFailed()
        {
            var (module, svc, _, _, bus, ctx) = BuildSpine();
            Run(module.Setup(ctx));

            SectorResult? got = null;
            module.SectorEndRequested += r => got = r;

            svc.FailSpine();
            svc.Tick(0.1f);

            Assert.IsTrue(got.HasValue, "The failed terminal state must end the sector.");
            Assert.IsFalse(got.Value.Success);
            Assert.AreEqual("spine_failed", got.Value.FailReason);
            Assert.IsTrue(bus.Get(SectorSpineModule.TokenPrefix + SectorSpineModule.StepFailed));
        }

        [Test]
        public void Teardown_ClearsSpine_AndUnsubscribesFromStepEvents()
        {
            var (module, svc, _, _, bus, ctx) = BuildSpine();
            Run(module.Setup(ctx));

            Run(module.Teardown(ctx));

            Assert.IsNull(svc.SpineTracker, "Teardown must clear the spine.");
            Assert.IsNull(svc.SpineTarget);

            InstallPostSpineMission(svc);
            Assert.IsNull(svc.SpineTarget,
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
