using System.Collections.Generic;
using Game.Services;
using NUnit.Framework;
using UI;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Spine target channel: the service carries SpineTarget + OnSpineTargetChanged and the minimap marker binds to it.</summary>
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
        public void SetSpineTarget_UpdatesSpineTarget_AndRaisesOnChangeOnly()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var t = NewGO("Target").transform;

            var raised = 0;
            Transform last = null;
            ((IObjectiveService)svc).OnSpineTargetChanged += x => { raised++; last = x; };

            svc.SetSpineTarget(t);
            Assert.AreEqual(t, svc.SpineTarget);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(t, last);

            svc.SetSpineTarget(t);
            Assert.AreEqual(1, raised, "Setting the same target must not re-raise OnSpineTargetChanged.");

            svc.SetSpineTarget(null);
            Assert.IsNull(svc.SpineTarget);
            Assert.AreEqual(2, raised);
        }

        [Test]
        public void Marker_Bind_SubscribesToChannel_WithoutThrowing()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var marker = NewGO("Marker").AddComponent<MinimapObjectiveMarker>();
            var t = NewGO("Target").transform;

            Assert.DoesNotThrow(() =>
            {
                marker.BindObjectiveService(svc);
                svc.SetSpineTarget(t);
                svc.SetSpineTarget(null);
                marker.BindObjectiveService(svc);
            });
        }
    }
}
