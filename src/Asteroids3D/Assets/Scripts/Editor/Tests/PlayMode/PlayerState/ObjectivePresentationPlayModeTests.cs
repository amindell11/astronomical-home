using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Objectives;
using Substrate.Services.Objectives;
using UI.Objectives;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.PlayerState
{
    [Category("UI")]
    public class ObjectivePresentationPlayModeTests
    {
        private GameObject root;
        private ObjectiveService service;
        private Canvas canvas;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Objective presentation");
            service = root.AddComponent<ObjectiveService>();
            canvas = Child("HUD").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private GameObject Child(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private SpineObjectiveHandle Open(Transform target) => service.SetSpineObjective(
            new MissionDefinition("extract", new Dictionary<string, string>()),
            new Dictionary<string, Func<ObjectiveState>> { ["extract"] = () => new ExtractionState() }, target);

        [UnityTest]
        public IEnumerator Opening_FadesToStatus_AndReplaysForANewSectorOnly()
        {
            var readout = ObjectiveStatusReadout.Create(canvas.transform);
            readout.Initialize(service);
            var label = readout.GetComponentInChildren<Text>();
            var target = Child("Destination").transform;
            var handle = Open(target);
            yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual("Extraction open", label.text);
            Assert.AreEqual(1f, readout.GetComponent<CanvasGroup>().alpha, .01f);
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.AreEqual("PRESS F TO JUMP", label.text, "Contextual interaction survives the announcement");
            handle.Target = Child("Replacement destination").transform;
            yield return null;
            Assert.AreEqual("PRESS F TO JUMP", label.text, "Retargeting cannot replay the opening");
            Open(target);
            yield return null;
            Assert.AreEqual("Extraction open", label.text, "A new sector opens without a blank frame between trackers");
            service.ClearAll();
            yield return null;
            Assert.IsEmpty(label.text);
        }

        [UnityTest]
        public IEnumerator Markers_StaySmallAcrossZoom_AndHideWhenTheTargetIsDestroyed()
        {
            var camera = Child("World camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -50);
            camera.orthographic = true;
            camera.orthographicSize = 18;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var minimap = (RectTransform)Child("Minimap").transform;
            minimap.SetParent(canvas.transform, false);
            minimap.anchorMin = minimap.anchorMax = Vector2.one;
            minimap.anchoredPosition = new Vector2(-120, -120);
            minimap.sizeDelta = new Vector2(200, 200);
            var icon = Child("Legacy objective").AddComponent<Image>();
            var markers = Child("Markers").AddComponent<ObjectiveMarkers>();
            typeof(ObjectiveMarkers).GetField("icon", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(markers, icon);
            markers.Initialize(service, camera, camera, minimap, canvas);
            var target = Child("Destination").transform;
            target.position = new Vector3(8, 6, 0);
            Open(target);
            yield return null;
            var screen = canvas.transform.Find("Extraction direction").GetComponent<BerthGraphic>();
            var map = canvas.transform.Find("Extraction minimap").GetComponent<BerthGraphic>();
            Assert.IsTrue(screen.enabled && map.enabled);
            Assert.IsFalse(icon.enabled);
            var size = screen.rectTransform.rect.size;
            camera.orthographicSize = 60;
            yield return null;
            Assert.AreEqual(size, screen.rectTransform.rect.size);
            Assert.AreEqual(Vector3.one, screen.rectTransform.localScale);
            Assert.Less(map.rectTransform.rect.width, screen.rectTransform.rect.width);
            Assert.AreEqual(Quaternion.identity, map.rectTransform.localRotation);
            var mapPosition = map.rectTransform.anchoredPosition;
            camera.transform.position += new Vector3(10, 7, 0);
            target.position += new Vector3(10, 7, 0);
            yield return null;
            Assert.That(Vector2.Distance(mapPosition, map.rectTransform.anchoredPosition), Is.LessThan(.1f),
                "Moving the camera and destination together cannot shift the icon inside the minimap");
            Object.Destroy(target.gameObject);
            yield return null;
            yield return null;
            Assert.IsFalse(screen.enabled || map.enabled || icon.enabled);
        }

        private sealed class ExtractionState : ObjectiveState
        {
            public override ObjectiveType StateType => ObjectiveType.ExtractionChallenge;
            public override bool IsComplete => false;
            public override string StatusLine => "PRESS F TO JUMP";
            public override void Tick(float deltaTime) { }
        }
    }
}
