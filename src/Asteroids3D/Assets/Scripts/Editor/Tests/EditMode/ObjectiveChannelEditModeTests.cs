using System.Collections.Generic;
using Game.Services;
using NUnit.Framework;
using UI;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the objective-marker channel (Stage 2.3a): the service carries a
    /// CurrentTarget + OnTargetChanged event, and the minimap marker binds to it and self-decides
    /// visibility. Pure C# / lightweight GameObjects — no sector or prefab construction.
    /// </summary>
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

        [Test]
        public void SetTarget_UpdatesCurrentTarget_AndRaisesOnChangeOnly()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var t = NewGO("Target").transform;

            var raised = 0;
            Transform last = null;
            ((IObjectiveService)svc).OnTargetChanged += x => { raised++; last = x; };

            svc.SetTarget(t);
            Assert.AreEqual(t, svc.CurrentTarget);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(t, last);

            // Same value → no re-raise.
            svc.SetTarget(t);
            Assert.AreEqual(1, raised, "Setting the same target must not re-raise OnTargetChanged.");

            svc.SetTarget(null);
            Assert.IsNull(svc.CurrentTarget);
            Assert.AreEqual(2, raised);
        }

        [Test]
        public void Marker_Bind_SubscribesToChannel_WithoutThrowing()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var marker = NewGO("Marker").AddComponent<MinimapObjectiveMarker>();
            var t = NewGO("Target").transform;

            // Binding subscribes the marker to the channel; subsequent target changes (and a
            // re-bind, which must unsubscribe the prior handler) flow without error.
            Assert.DoesNotThrow(() =>
            {
                marker.BindObjectiveService(svc);
                svc.SetTarget(t);
                svc.SetTarget(null);
                marker.BindObjectiveService(svc);
            });
        }
    }
}
