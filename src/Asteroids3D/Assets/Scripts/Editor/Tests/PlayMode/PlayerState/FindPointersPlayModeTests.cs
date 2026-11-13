using System.Collections;
using NUnit.Framework;
using Substrate.Services.Objectives;
using Tests.PlayMode.Common;
using UI.Objectives;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.PlayerState
{
    /// <summary>A find beyond the minimap's range points from the rim toward it with its icon; in range, or closed, it shows no pointer.</summary>
    [Category("UI")]
    [Category("RequiresLfsPng")]
    public class FindPointersPlayModeTests
    {
        private const string IconPath = "Assets/Visuals/Ui/Minimap/Finds/RepairFind.png";

        private GameObject root;
        private ObjectiveService service;
        private RectTransform minimap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Find pointers");
            service = root.AddComponent<ObjectiveService>();
            var canvas = Child("HUD").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            minimap = (RectTransform)Child("Minimap").transform;
            minimap.SetParent(canvas.transform, false);
            minimap.sizeDelta = new Vector2(200, 200);

            var camera = Child("Minimap camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -50);
            camera.orthographic = true;
            camera.orthographicSize = 56;
            camera.aspect = 1f;
            camera.enabled = false;
            FindPointers.Create(minimap).Initialize(service, camera);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        private GameObject Child(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            return child;
        }

        [UnityTest]
        public IEnumerator AFindBeyondRange_PointsFromTheRim_AndInRangeOrClosed_ShowsNone()
        {
            var icon = TestAssets.Load<Sprite>(IconPath);
            var target = Child("Station").transform;
            target.position = new Vector3(200, 100, 0);
            var colour = Color.cyan;
            var find = service.MarkFind(target, icon, colour);
            yield return null;

            var pointer = minimap.Find("Find pointers/Find pointer").GetComponent<Image>();
            var arrow = minimap.Find("Find pointers/Find pointer arrow").GetComponent<NavTab>();
            var direction = new Vector2(200, 100).normalized;
            Assert.IsTrue(pointer.enabled && arrow.enabled, "A find beyond range shows a pointer.");
            Assert.AreSame(icon, pointer.sprite, "The pointer shows the find's icon.");
            Assert.AreEqual(colour, pointer.color, "The icon takes the find's colour.");
            Assert.AreEqual(colour, arrow.color, "The arrowhead takes the find's colour.");
            Assert.Less(Vector2.Angle(pointer.rectTransform.anchoredPosition, direction), .5f, "The pointer sits toward the find.");
            Assert.That(pointer.rectTransform.anchoredPosition.magnitude, Is.InRange(70f, 100f), "The pointer sits on the rim.");
            Assert.Greater(arrow.rectTransform.anchoredPosition.magnitude, pointer.rectTransform.anchoredPosition.magnitude,
                "The arrowhead sits outside the icon.");
            Assert.Less(Vector2.Angle(arrow.rectTransform.localRotation * Vector3.up, direction), .5f,
                "The arrowhead aims at the find.");

            target.position = new Vector3(20, 10, 0);
            yield return null;
            Assert.IsFalse(pointer.enabled || arrow.enabled, "In range the find's own marker shows instead.");

            target.position = new Vector3(-150, 0, 0);
            yield return null;
            Assert.IsTrue(pointer.enabled);
            Assert.Less(Vector2.Angle(pointer.rectTransform.anchoredPosition, Vector2.left), .5f);

            find.Close();
            yield return null;
            Assert.IsFalse(pointer.enabled || arrow.enabled, "A closed find drops its pointer.");
        }
    }
}
