using System;
using System.Collections.Generic;
using Game.Services;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
    /// <summary>Spine target channel: the owner's handle drives SpineTarget + OnSpineTargetChanged and the minimap marker binds to it.</summary>
    [TestFixture]
    [Category("Objectives")]
    public class ObjectiveChannelEditModeTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject NewGO(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private static SpineObjectiveHandle InstallSpine(ObjectiveService svc) =>
            svc.SetSpineObjective(
                new MissionDefinition("run", new Dictionary<string, string>()),
                new Dictionary<string, Func<ObjectiveState>> { ["run"] = () => new CompletedState() });

        [Test]
        public void HandleTargetSet_UpdatesSpineTarget_AndRaisesOnChangeOnly()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var handle = InstallSpine(svc);
            var t = NewGO("Target").transform;

            var raised = 0;
            Transform last = null;
            ((IObjectiveService)svc).OnSpineTargetChanged += x => { raised++; last = x; };

            handle.Target = t;
            Assert.AreEqual(t, svc.SpineTarget);
            Assert.AreEqual(t, handle.Target);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(t, last);

            handle.Target = t;
            Assert.AreEqual(1, raised, "Setting the same target must not re-raise OnSpineTargetChanged.");

            handle.Target = null;
            Assert.IsNull(svc.SpineTarget);
            Assert.AreEqual(2, raised);
        }

        [Test]
        public void Marker_Bind_SubscribesToChannel_WithoutThrowing()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var handle = InstallSpine(svc);
            var marker = NewGO("Marker").AddComponent<MinimapObjectiveMarker>();
            var t = NewGO("Target").transform;

            Assert.DoesNotThrow(() =>
            {
                marker.BindObjectiveService(svc);
                handle.Target = t;
                handle.Target = null;
                marker.BindObjectiveService(svc);
            });
        }
    }
}
