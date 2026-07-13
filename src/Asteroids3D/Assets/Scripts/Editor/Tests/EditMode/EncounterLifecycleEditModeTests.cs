using System;
using System.Collections;
using System.Collections.Generic;
using Game.Encounters;
using Game.Services;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Encounter base subscription hygiene against the spine channel across teardown/destroy paths.</summary>
    [TestFixture]
    [Category("Objectives")]
    public class EncounterLifecycleEditModeTests
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

        private sealed class TargetReadCounter
        {
            public int Reads;
        }

        private sealed class ProbeEncounter : Encounter
        {
            public TargetReadCounter Counter;

            public override Transform ObjectiveTarget
            {
                get
                {
                    Counter.Reads++;
                    return null;
                }
            }

            protected override IEnumerator OnSetup() { yield break; }
            protected override IEnumerator OnTeardown() { yield break; }
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

        private sealed class Flag
        {
            public bool Done;
        }

        private sealed class FlagState : ObjectiveState
        {
            private readonly Flag flag;
            public FlagState(Flag flag) => this.flag = flag;
            public override ObjectiveType StateType => ObjectiveType.Explore;
            public override void Tick(float deltaTime) { }
            public override bool IsComplete => flag.Done;
        }

        private static void SetSpineRunToDone(ObjectiveService svc, Flag flag)
        {
            svc.SetSpineObjective(
                new MissionDefinition("run", new Dictionary<string, string> { { "run", "done" } }),
                new Dictionary<string, Func<ObjectiveState>>
                {
                    ["run"] = () => new FlagState(flag),
                    ["done"] = () => new CompletedState()
                });
        }

        private static void Run(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }

        private ProbeEncounter SetUpEncounter(ObjectiveService svc, TargetReadCounter counter)
        {
            var enc = NewGO("Encounter").AddComponent<ProbeEncounter>();
            enc.Counter = counter;
            enc.Initialize(new StubServices(svc), null);
            Run(enc.Setup());
            return enc;
        }

        [Test]
        public void Encounter_DestroyedWithoutTeardown_StopsReceivingSpineCallbacks()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var counter = new TargetReadCounter();
            var enc = SetUpEncounter(svc, counter);

            var flag = new Flag();
            SetSpineRunToDone(svc, flag);
            var readsBeforeTransition = counter.Reads;
            flag.Done = true;
            svc.Tick(0.1f);
            Assert.Greater(counter.Reads, readsBeforeTransition,
                "Sanity: a live encounter re-reports its target on spine transitions.");

            UnityEngine.Object.DestroyImmediate(enc.gameObject);
            var readsAfterDestroy = counter.Reads;

            var nextFlag = new Flag();
            SetSpineRunToDone(svc, nextFlag);
            nextFlag.Done = true;
            svc.Tick(0.1f);

            Assert.AreEqual(readsAfterDestroy, counter.Reads,
                "An encounter destroyed without Teardown must not receive spine callbacks.");
        }

        [Test]
        public void Encounter_TeardownThenDestroy_DoesNotThrow()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var enc = SetUpEncounter(svc, new TargetReadCounter());

            Run(enc.Teardown());
            Assert.DoesNotThrow(() => UnityEngine.Object.DestroyImmediate(enc.gameObject));
        }
    }
}
