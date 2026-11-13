using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ships;
using Substrate.Services.Objectives;
using Tests.PlayMode.Common;
using UI.Objectives;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Utils;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.PlayerState
{
    /// <summary>
    /// A find beyond the minimap's range points from the rim toward it with its icon; in range, or closed, it shows no pointer.
    /// A live unconcealed enemy beyond range points from the rim with only an arrowhead; in range, concealed, inactive or gone, it shows none.
    /// Off the world camera's view, the same finds and enemies get a screen-edge pointer; a target in view, the player, and a concealed or destroyed ship get none.
    /// </summary>
    [Category("UI")]
    [Category("RequiresLfsPng")]
    public class FindPointersPlayModeTests
    {
        private const string IconPath = "Assets/Visuals/Ui/Minimap/Finds/RepairFind.png";
        private static readonly Color EnemyColour = new Color(1f, .2f, .1f, 1f);

        private GameObject root;
        private ObjectiveService service;
        private Canvas canvas;
        private RectTransform minimap;
        private SubscribedSet<Ship> ships;
        private Ship ship;
        private Ship player;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Find pointers");
            service = root.AddComponent<ObjectiveService>();
            canvas = Child("HUD").AddComponent<Canvas>();
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

            var worldCamera = Child("World camera").AddComponent<Camera>();
            worldCamera.transform.position = Vector3.zero;
            worldCamera.transform.rotation = Quaternion.identity;
            worldCamera.fieldOfView = 60f;
            worldCamera.aspect = 1f;
            worldCamera.enabled = false;
            ships = new SubscribedSet<Ship>();
            FindPointers.Create(minimap).Initialize(service, camera, worldCamera, canvas, ships, EnemyColour);
        }

        [TearDown]
        public void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            ShipTestFactory.DestroyShip(player);
            Object.DestroyImmediate(root);
        }

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

        [UnityTest]
        public IEnumerator AnEnemyBeyondRange_PointsFromTheRim_AndInRangeConcealedOrGone_ShowsNone()
        {
            ship =ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero);
            ship.transform.position = new Vector3(0f, 200f, 0f);
            ships.Add(ship);
            player = ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero);
            player.tag = "Player";
            player.transform.position = new Vector3(0f, -200f, 0f);
            ships.Add(player);

            yield return null;
            var shown = VisiblePointers();
            Assert.AreEqual(1, shown.arrows.Count, "An enemy beyond range shows one arrowhead.");
            Assert.AreEqual(EnemyColour, shown.arrows[0].color);
            Assert.Less(Vector2.Angle(shown.arrows[0].rectTransform.anchoredPosition, Vector2.up), .5f,
                "The arrowhead sits toward the enemy.");
            Assert.That(shown.arrows[0].rectTransform.anchoredPosition.magnitude, Is.InRange(70f, 100f),
                "The arrowhead sits on the rim.");
            Assert.AreEqual(0, shown.icons.Count, "An enemy pointer has no icon.");

            ship.transform.position = new Vector3(0f, 20f, 0f);
            yield return null;
            Assert.AreEqual(0, VisiblePointers().arrows.Count, "In range an enemy shows no pointer.");

            ship.transform.position = new Vector3(200f, 0f, 0f);
            yield return null;
            shown = VisiblePointers();
            Assert.AreEqual(1, shown.arrows.Count);
            Assert.Less(Vector2.Angle(shown.arrows[0].rectTransform.anchoredPosition, Vector2.right), .5f,
                "The arrowhead aims right.");

            ship.Lock.Conceal();
            yield return null;
            Assert.AreEqual(0, VisiblePointers().arrows.Count, "A concealed enemy shows no pointer.");
            ship.Lock.Reveal();
            yield return null;
            Assert.AreEqual(1, VisiblePointers().arrows.Count, "A revealed enemy shows its pointer again.");

            ship.gameObject.SetActive(false);
            yield return null;
            Assert.AreEqual(0, VisiblePointers().arrows.Count, "An inactive enemy shows no pointer.");
            ship.gameObject.SetActive(true);
            yield return null;
            Assert.AreEqual(1, VisiblePointers().arrows.Count, "A reactivated enemy shows its pointer again.");

            ships.Remove(ship);
            Object.Destroy(ship.gameObject);
            yield return null;
            Assert.AreEqual(0, VisiblePointers().arrows.Count, "A removed enemy shows no pointer.");
        }

        [UnityTest]
        public IEnumerator AnEnemyOffScreen_GetsOneEdgePointerOnItsSide_AndInViewConcealedOrDestroyedGetsNone()
        {
            ship = ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero);
            ship.transform.position = new Vector3(300f, 0f, 100f);
            ships.Add(ship);
            player = ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero);
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 400f, 100f);
            ships.Add(player);
            yield return null;

            var holder = (RectTransform)canvas.transform.Find("Screen pointers");
            var shown = VisibleScreenPointers();
            Assert.AreEqual(1, shown.arrows.Count, "An enemy off the right edge shows one tab.");
            Assert.AreEqual(0, shown.icons.Count, "An enemy pointer has no icon.");
            Assert.AreEqual(EnemyColour, shown.arrows[0].color);
            Assert.Greater(shown.arrows[0].rectTransform.anchoredPosition.x, holder.rect.width * .5f,
                "The tab sits on the right half.");
            Assert.Less(Vector2.Angle(shown.arrows[0].rectTransform.localRotation * Vector3.up, Vector2.right), 1f,
                "The tab aims right.");

            ship.transform.position = new Vector3(-300f, 0f, 100f);
            yield return null;
            shown = VisibleScreenPointers();
            Assert.AreEqual(1, shown.arrows.Count, "An enemy off the left edge shows one tab.");
            Assert.Less(shown.arrows[0].rectTransform.anchoredPosition.x, holder.rect.width * .5f,
                "The tab sits on the left half.");
            Assert.Less(Vector2.Angle(shown.arrows[0].rectTransform.localRotation * Vector3.up, Vector2.left), 1f,
                "The tab aims left.");

            ship.transform.position = new Vector3(0f, 0f, -100f);
            yield return null;
            Assert.AreEqual(1, VisibleScreenPointers().arrows.Count, "An enemy behind the camera is off-screen.");

            ship.transform.position = new Vector3(0f, 0f, 100f);
            yield return null;
            Assert.AreEqual(0, VisibleScreenPointers().arrows.Count, "An enemy in view shows no screen pointer.");

            ship.transform.position = new Vector3(300f, 0f, 100f);
            yield return null;
            Assert.AreEqual(1, VisibleScreenPointers().arrows.Count, "Back off-screen the edge pointer returns.");
            ship.Lock.Conceal();
            yield return null;
            Assert.AreEqual(0, VisibleScreenPointers().arrows.Count, "A concealed enemy shows no screen pointer.");

            ships.Remove(ship);
            Object.Destroy(ship.gameObject);
            yield return null;
            Assert.AreEqual(0, VisibleScreenPointers().arrows.Count, "A removed enemy shows no screen pointer.");
        }

        [UnityTest]
        public IEnumerator AFindOffScreen_GetsAnEdgePointerWithItsIcon()
        {
            var icon = TestAssets.Load<Sprite>(IconPath);
            var target = Child("Station").transform;
            target.position = new Vector3(0f, 300f, 100f);
            var colour = Color.cyan;
            service.MarkFind(target, icon, colour);
            yield return null;

            var shown = VisibleScreenPointers();
            Assert.AreEqual(1, shown.arrows.Count, "A find above the view shows one screen pointer.");
            Assert.AreEqual(1, shown.icons.Count, "The screen pointer shows the find's icon.");
            Assert.AreSame(icon, shown.icons[0].sprite);
            Assert.AreEqual(colour, shown.icons[0].color, "The icon takes the find's colour.");
            Assert.Less(Vector2.Angle(shown.arrows[0].rectTransform.localRotation * Vector3.up, Vector2.up), 1f,
                "The tab aims up.");

            target.position = new Vector3(0f, 0f, 100f);
            yield return null;
            shown = VisibleScreenPointers();
            Assert.AreEqual(0, shown.arrows.Count, "A find in view shows no screen pointer.");
            Assert.AreEqual(0, shown.icons.Count);
        }

        private (List<NavTab> arrows, List<Image> icons) VisiblePointers() =>
            VisibleUnder(minimap.Find("Find pointers"));

        private (List<NavTab> arrows, List<Image> icons) VisibleScreenPointers() =>
            VisibleUnder(canvas.transform.Find("Screen pointers"));

        private static (List<NavTab> arrows, List<Image> icons) VisibleUnder(Transform holder)
        {
            var arrows = new List<NavTab>();
            var icons = new List<Image>();
            for (var i = 0; i < holder.childCount; i++)
            {
                var child = holder.GetChild(i);
                var arrow = child.GetComponent<NavTab>();
                if (arrow && arrow.enabled)
                    arrows.Add(arrow);
                var icon = child.GetComponent<Image>();
                if (icon && icon.enabled)
                    icons.Add(icon);
            }

            return (arrows, icons);
        }
    }
}
