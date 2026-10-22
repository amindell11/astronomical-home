#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    [Category("UI"), Category("RequiresGraphics")]
    public sealed class HangarPreviewFlightPlayModeTests
    {
        private static readonly string[] ShipPaths =
        {
            "Assets/Prefabs/Ships/Valis.prefab", "Assets/Prefabs/Ships/Nightshade.prefab",
            "Assets/Prefabs/Ships/Crimson.prefab",
        };

        // An arrival spends its first frames off-frame, before its path begins.
        private const float OnPath = .15f;

        private HangarPreviewStage stage;
        private Ship valis;
        private Ship nightshade;
        private GameObject screenRoot;
        private ItemSubset offer;

        [SetUp]
        public void SetUp()
        {
            valis = AssetDatabase.LoadAssetAtPath<Ship>(ShipPaths[0]);
            nightshade = AssetDatabase.LoadAssetAtPath<Ship>(ShipPaths[1]);
            stage = HangarPreviewStage.Create(null, new Vector2Int(256, 256), new Rect(0f, 0f, 1f, 1f), new[] { valis, nightshade });
        }

        [TearDown]
        public void TearDown()
        {
            if (stage) Object.Destroy(stage.gameObject);
            if (screenRoot) Object.Destroy(screenRoot);
            if (offer) Object.Destroy(offer);
        }

        [UnityTest]
        public IEnumerator ArrowButtons_MirrorTheHandoff_IncludingListWrap()
        {
            var screen = OpenScreen(valis, nightshade);
            var loadout = screen.loadout;
            yield return new WaitForSeconds(1.4f);

            foreach (var backwards in new[] { false, false, true, true })
            {
                var outgoing = Active().Single().transform;
                var start = outgoing.localPosition;
                (backwards ? screen.view.shipRow.previousButton : screen.view.shipRow.nextButton).onClick.Invoke();
                yield return new WaitForSeconds(OnPath);
                var incoming = Active().Single(f => f.transform != outgoing);
                var direction = backwards ? -1f : 1f;
                Assert.That(incoming.transform.localPosition.x * direction, Is.GreaterThan(0f),
                    backwards ? "Previous enters from the left" : "Next enters from the right");
                Assert.That(incoming.name, Is.EqualTo(loadout.Ship.name + "_Preview"));
                yield return new WaitForSeconds(.3f);
                Assert.That((outgoing.localPosition.x - start.x) * direction, Is.LessThan(0f));
                yield return new WaitForSeconds(.8f);
            }
        }

        [UnityTest]
        public IEnumerator Arrival_FacesViewer_AndUsesValisWingSweep()
        {
            Show(valis);
            yield return null;
            var flight = Active().Single();
            var wing = flight.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Upper wing right");
            var start = flight.transform.localPosition;
            yield return new WaitForSeconds(.65f);
            Assert.That(Quaternion.Angle(Quaternion.identity, wing.localRotation), Is.LessThan(.1f));
            yield return new WaitForSeconds(1.15f);
            Assert.That(Quaternion.Angle(Quaternion.identity, wing.localRotation), Is.EqualTo(20f).Within(.1f));
            Assert.That(flight.transform.localPosition.magnitude, Is.LessThan(start.magnitude * .05f));
            var camera = stage.GetComponentInChildren<Camera>();
            Assert.That(Vector3.Dot(flight.transform.up, -camera.transform.forward), Is.GreaterThan(.75f));
            var settled = flight.transform.localRotation;
            yield return new WaitForSeconds(.7f);
            Assert.That(Quaternion.Angle(settled, flight.transform.localRotation), Is.InRange(.5f, 12f));
        }

        [UnityTest]
        public IEnumerator Switch_PeelsLeft_AndJoinsFromRight_WithoutMovingCamera()
        {
            Show(valis);
            yield return new WaitForSeconds(1.8f);
            var outgoing = Active().Single().transform;
            var start = outgoing.localPosition;
            var camera = stage.GetComponentInChildren<Camera>();
            var cameraPosition = camera.transform.position;
            Show(nightshade);
            yield return new WaitForSeconds(OnPath);
            var incoming = Active().Single(f => f.transform != outgoing);
            Assert.That(incoming.transform.localPosition.x, Is.GreaterThan(start.x));
            var right = incoming.transform.localPosition.x;
            yield return new WaitForSeconds(.35f);
            Assert.That(outgoing.localPosition.x, Is.LessThan(start.x));
            Assert.That(outgoing.localPosition.z, Is.GreaterThan(start.z));
            Assert.That(incoming.transform.localPosition.x, Is.LessThan(right));
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            yield return new WaitForSeconds(1f);
            Assert.That(Active(), Is.EqualTo(new[] { incoming }), "the departed clone is pooled out of view");
        }

        [UnityTest]
        public IEnumerator TenHertzBursts_KeepAShipInFrame_AndSelectOnThePressFrame()
        {
            var ships = ShipPaths.Select(AssetDatabase.LoadAssetAtPath<Ship>).ToArray();
            var screen = OpenScreen(ships);
            var loadout = screen.loadout;
            var camera = stage.GetComponentInChildren<Camera>();
            var pool = stage.GetComponentsInChildren<HangarPreviewFlight>(true);
            yield return new WaitForSeconds(1.4f);

            var index = 0;
            foreach (var step in Enumerable.Repeat(1, 15).Concat(Enumerable.Repeat(-1, 15)))
            {
                (step < 0 ? screen.view.shipRow.previousButton : screen.view.shipRow.nextButton).onClick.Invoke();
                index = (index + step + ships.Length) % ships.Length;
                Assert.That(loadout.Ship, Is.SameAs(ships[index]), "the selection follows the press on its frame");
                Assert.That(screen.view.shipRow.label.text, Is.EqualTo(ships[index].name));
                for (var end = Time.time + .1f; Time.time < end;)
                {
                    yield return null;
                    AssertAShipInFrame(camera, screen.view);
                }
            }
            for (var end = Time.time + 1.5f; Time.time < end;)
            {
                yield return null;
                AssertAShipInFrame(camera, screen.view);
            }

            Assert.That(stage.GetComponentsInChildren<HangarPreviewFlight>(true), Is.EquivalentTo(pool),
                "the burst reuses the pooled clones");
            Assert.That(Active().Single().name, Is.EqualTo(ships[index].name + "_Preview"));
        }

        // The stage camera's viewport is the preview image; the framing square is where the ship settles.
        private void AssertAShipInFrame(Camera camera, HangarScreen screen)
        {
            var image = new Vector3[4];
            var square = new Vector3[4];
            screen.previewImage.rectTransform.GetWorldCorners(image);
            screen.previewFrame.GetWorldCorners(square);
            var size = (Vector2)(image[2] - image[0]);
            var min = (Vector2)(square[0] - image[0]) / size;
            var max = (Vector2)(square[2] - image[0]) / size;
            var frame = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            Assert.That(Active().Any(f => frame.Overlaps(ViewportBounds(camera, f))),
                "some ship overlaps the preview on frame {0}", Time.frameCount);
        }

        private static Rect ViewportBounds(Camera camera, Component flight)
        {
            var bounds = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            foreach (var r in flight.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1));
                    var p = camera.WorldToViewportPoint(corner);
                    bounds.xMin = Mathf.Min(bounds.xMin, p.x);
                    bounds.yMin = Mathf.Min(bounds.yMin, p.y);
                    bounds.xMax = Mathf.Max(bounds.xMax, p.x);
                    bounds.yMax = Mathf.Max(bounds.yMax, p.y);
                }
            }
            return bounds;
        }

        private (HangarScreen view, ShipLoadout loadout) OpenScreen(params Ship[] ships)
        {
            Object.DestroyImmediate(stage.gameObject);
            screenRoot = new GameObject("ScreenRoot");
            var prefab = AssetDatabase.LoadAssetAtPath<HangarScreen>("Assets/Prefabs/UI/HangarScreen.prefab");
            var screen = Object.Instantiate(prefab, screenRoot.transform);
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.ships = ships;
            var loadout = new ShipLoadout(ships[0], null, null, null);
            screen.Show(offer, loadout, null);
            stage = screenRoot.GetComponentInChildren<HangarPreviewStage>();
            return (screen, loadout);
        }

        private HangarPreviewFlight[] Active() => stage.GetComponentsInChildren<HangarPreviewFlight>();

        private void Show(Ship ship, bool backwards = false) =>
            stage.Show(new ShipLoadout(ship, null, null, null), backwards);
    }
}
#endif
