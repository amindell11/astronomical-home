using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Objectives;
using Objectives.States;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Substrate.Services.Objectives;

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
                new Dictionary<string, Func<ObjectiveState>> { ["run"] = () => new KeyAcquiredState() });

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
        public void Marker_Bind_ShowsATargetPublishedAfterBinding()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();
            var handle = InstallSpine(svc);
            var cam = NewGO("MinimapCam").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 50f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            var minimap = NewGO("Minimap").AddComponent<RectTransform>();
            minimap.sizeDelta = new Vector2(200f, 200f);
            var icon = NewGO("Icon").AddComponent<Image>();
            var marker = NewGO("Marker").AddComponent<MinimapObjectiveMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            marker.Initialize(cam, minimap);
            var lateUpdate = typeof(MinimapObjectiveMarker)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

            marker.BindObjectiveService(svc);
            var target = NewGO("Target").transform;
            target.position = new Vector3(0f, 25f, 0f);
            handle.Target = target;
            lateUpdate.Invoke(marker, null);

            Assert.IsTrue(icon.enabled, "a target published after binding must show on the minimap");
            Assert.AreEqual(50f, icon.rectTransform.anchoredPosition.y, 0.01f,
                "the icon sits at the target's place on the minimap");

            handle.Target = null;
            lateUpdate.Invoke(marker, null);

            Assert.IsFalse(icon.enabled, "clearing the target must hide the icon");
        }
    }
}
