using System;
using System.Collections.Generic;
using Game.Services;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UnityEngine;
using Game.Services.Objectives;

namespace Tests.EditMode
{
    /// <summary>Two-tier objective service: concurrent encounter-owned locals alongside the sector spine.</summary>
    [TestFixture]
    [Category("Objectives")]
    public class ObjectiveServiceTwoTierEditModeTests
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

        private ObjectiveService NewService() =>
            NewGO("ObjectiveService").AddComponent<ObjectiveService>();

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

        private static MissionDefinition RunToDoneMission() =>
            new("run", new Dictionary<string, string> { { "run", "done" } });

        private static Dictionary<string, Func<ObjectiveState>> RunToDoneBuilders(
            Flag flag, Action onDone = null)
        {
            return new Dictionary<string, Func<ObjectiveState>>
            {
                ["run"] = () => new FlagState(flag),
                ["done"] = () => new CompletedState(onEnter: onDone)
            };
        }

        [Test]
        public void OpenLocal_ReturnsHandle_WithTrackerAndTarget()
        {
            var svc = NewService();
            var target = NewGO("Target").transform;

            var handle = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()), target);

            Assert.IsNotNull(handle.Tracker);
            Assert.AreEqual(ObjectiveType.Explore, handle.Tracker.CurrentState);
            Assert.AreEqual(target, handle.Target);
            Assert.AreEqual(1, svc.Locals.Count);
            Assert.AreSame(handle, svc.Locals[0]);

            var other = NewGO("Other").transform;
            handle.Target = other;
            Assert.AreEqual(other, handle.Target);
        }

        [Test]
        public void OpenLocal_TickToCompletion_OnEnterCloseRemovesHandle()
        {
            var svc = NewService();
            var flag = new Flag();
            LocalObjectiveHandle handle = null;
            handle = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(flag, () => handle.Close()));

            svc.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Explore, handle.Tracker.CurrentState);
            Assert.AreEqual(1, svc.Locals.Count);

            flag.Done = true;
            svc.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Completed, handle.Tracker.CurrentState);
            Assert.AreEqual(0, svc.Locals.Count, "Completion-driven Close must remove the handle.");
        }

        [Test]
        public void TwoLocals_TickIndependently()
        {
            var svc = NewService();
            var flagA = new Flag();
            var flagB = new Flag();
            var a = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(flagA));
            var b = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(flagB));

            flagA.Done = true;
            svc.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Completed, a.Tracker.CurrentState);
            Assert.AreEqual(ObjectiveType.Explore, b.Tracker.CurrentState);

            flagB.Done = true;
            svc.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Completed, b.Tracker.CurrentState);
            Assert.AreEqual(2, svc.Locals.Count);
        }

        [Test]
        public void Close_RemovesLocal_AndServiceStopsTickingIt()
        {
            var svc = NewService();
            var flag = new Flag();
            var handle = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(flag));

            handle.Close();
            Assert.AreEqual(0, svc.Locals.Count);

            flag.Done = true;
            svc.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Explore, handle.Tracker.CurrentState,
                "A closed local must no longer be ticked by the service.");
        }

        [Test]
        public void Close_Twice_IsIdempotent()
        {
            var svc = NewService();
            var handle = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));

            var raised = 0;
            svc.OnLocalsChanged += () => raised++;

            handle.Close();
            Assert.AreEqual(1, raised);

            Assert.DoesNotThrow(() => handle.Close());
            Assert.AreEqual(1, raised, "Double-close must not re-raise OnLocalsChanged.");
        }

        [Test]
        public void OnLocalsChanged_FiresOnOpenAndClose()
        {
            var svc = NewService();
            var raised = 0;
            svc.OnLocalsChanged += () => raised++;

            var a = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));
            Assert.AreEqual(1, raised);

            svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));
            Assert.AreEqual(2, raised);

            a.Close();
            Assert.AreEqual(3, raised);
        }

        [Test]
        public void SpineAndLocals_Coexist_WithoutInterference()
        {
            var svc = NewService();
            var spineFlag = new Flag();
            var localFlag = new Flag();

            svc.SetSpineObjective(RunToDoneMission(), RunToDoneBuilders(spineFlag));
            var local = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(localFlag));

            var spineTransitions = 0;
            svc.OnSpineStateChanged += (_, _) => spineTransitions++;

            localFlag.Done = true;
            svc.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Completed, local.Tracker.CurrentState);
            Assert.AreEqual(ObjectiveType.Explore, svc.SpineState);
            Assert.AreEqual(0, spineTransitions, "Local transitions must not raise spine events.");

            spineFlag.Done = true;
            svc.Tick(0.1f);
            Assert.AreEqual(ObjectiveType.Completed, svc.SpineState);
            Assert.AreEqual(1, spineTransitions);
            Assert.AreEqual(1, svc.Locals.Count, "Spine transitions must not close locals.");
        }

        [Test]
        public void ClearAll_HandlerClosingAnotherLocal_MidSweep_StillEmpties()
        {
            var svc = NewService();
            svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));
            svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));
            svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));

            var closedOther = false;
            svc.OnLocalsChanged += () =>
            {
                if (closedOther || svc.Locals.Count == 0) return;
                closedOther = true;
                svc.Locals[0].Close();
            };

            Assert.DoesNotThrow(() => svc.ClearAll());
            Assert.AreEqual(0, svc.Locals.Count);
        }

        [Test]
        public void ClearAll_HandlerOpeningNewLocal_MidSweep_NewLocalAlsoClosed()
        {
            var svc = NewService();
            svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));

            var openedOne = false;
            LocalObjectiveHandle opened = null;
            svc.OnLocalsChanged += () =>
            {
                if (openedOne) return;
                openedOne = true;
                opened = svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));
            };

            Assert.DoesNotThrow(() => svc.ClearAll());
            Assert.IsNotNull(opened);
            Assert.AreEqual(0, svc.Locals.Count,
                "A local opened by a handler during ClearAll must be closed before ClearAll returns.");
        }

        [Test]
        public void ClearAll_ClosesLocalsAndSpine()
        {
            var svc = NewService();
            var spine = svc.SetSpineObjective(RunToDoneMission(), RunToDoneBuilders(new Flag()));
            spine.Target = NewGO("SpineTarget").transform;
            svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));
            svc.OpenLocal(RunToDoneMission(), RunToDoneBuilders(new Flag()));

            svc.ClearAll();

            Assert.IsNull(svc.SpineTracker);
            Assert.IsNull(svc.SpineState);
            Assert.IsNull(svc.SpineTarget);
            Assert.AreEqual(0, svc.Locals.Count);
        }

        [Test]
        public void SetSpineObjective_WithTarget_PublishesTargetOnInstall()
        {
            var svc = NewService();
            var target = NewGO("Target").transform;

            Transform seen = null;
            ((IObjectiveService)svc).OnSpineTargetChanged += t => seen = t;

            svc.SetSpineObjective(RunToDoneMission(), RunToDoneBuilders(new Flag()), target);

            Assert.AreEqual(target, svc.SpineTarget);
            Assert.AreEqual(target, seen);
        }

        [Test]
        public void StaleSpineHandle_MutationsAreNoOps_OnSuccessorSpine()
        {
            var svc = NewService();
            var first = svc.SetSpineObjective(RunToDoneMission(), RunToDoneBuilders(new Flag()));
            var second = svc.SetSpineObjective(RunToDoneMission(), RunToDoneBuilders(new Flag()));

            var target = NewGO("Target").transform;
            second.Target = target;

            first.Close();
            Assert.AreSame(second.Tracker, svc.SpineTracker,
                "A stale handle's Close must not clear the successor spine.");
            Assert.AreEqual(target, svc.SpineTarget);

            first.Target = NewGO("Other").transform;
            Assert.AreEqual(target, svc.SpineTarget, "A stale handle's target-set must be a no-op.");
            Assert.IsNull(first.Target, "A stale handle must not read the successor's target.");

            second.Close();
            Assert.IsNull(svc.SpineTracker);
        }
    }
}
