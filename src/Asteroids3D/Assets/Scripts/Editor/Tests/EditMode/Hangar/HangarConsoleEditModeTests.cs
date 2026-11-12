using System.Linq;
using NUnit.Framework;
using UI.Hangar.Displays;
using UI.Hangar.Hardware;
using UI.Hangar.Selection;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Hangar
{
    [Category("UI")]
    public sealed class HangarConsoleEditModeTests
    {
        private const string PrefabPath = "Assets/Prefabs/UI/HangarConsole.prefab";

        [Test]
        public void ControlsHaveIndependentUnitScalePivotsAndRealMeshes()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            var parts = new[]
            {
                console.MainScreenGlass.transform, console.DetailScreenGlass.transform,
                console.SelectorKnob, console.PreviousButton, console.NextButton,
                console.LaunchButton, console.ProjectorLens.transform
            };
            Assert.That(parts.Select(part => part.name), Is.EquivalentTo(new[]
            {
                "MainScreenGlass", "DetailScreenGlass", "SelectorKnob", "PreviousButton",
                "NextButton", "LaunchButton", "ProjectorLens"
            }));
            Assert.That(parts.Distinct().Count(), Is.EqualTo(7));
            foreach (var part in parts)
            {
                Assert.That(Vector3.Distance(part.lossyScale, Vector3.one), Is.LessThan(.001f), part.name);
                var mesh = part.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(8), part.name);
                Assert.That(mesh.bounds.center.magnitude, Is.LessThan(.5f), part.name + " pivot");
            }
        }

        [TestCase(1920, 1080)]
        [TestCase(1920, 1200)]
        [TestCase(2520, 1080)]
        public void AllControlsRemainInsideTheFrame(int width, int height)
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath));
            try
            {
                instance.Resize(new Vector2Int(width, height));
                var camera = instance.HardwareCamera;
                camera.aspect = (float)width / height;
                foreach (var control in new[] { instance.SelectorKnob, instance.PreviousButton,
                             instance.NextButton, instance.LaunchButton, instance.ProjectorLens.transform })
                {
                    var point = camera.WorldToViewportPoint(control.position);
                    Assert.That(point.x, Is.InRange(.01f, .99f), control.name);
                    Assert.That(point.y, Is.InRange(.01f, .99f), control.name);
                    Assert.That(point.z, Is.GreaterThan(0f), control.name);
                }
                Assert.That(instance.Texture.width, Is.EqualTo(width));
                Assert.That(instance.Texture.height, Is.EqualTo(height));
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void TallerViewKeepsHeaderAndDeckCoveringTheFrame()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath));
            try
            {
                instance.Resize(new Vector2Int(1920, 1080));
                var camera = instance.HardwareCamera;
                var top = camera.ViewportToWorldPoint(new Vector3(.5f, 1f, 10f));
                var bottom = camera.ViewportToWorldPoint(new Vector3(.5f, 0f, 10f));
                // Source-camera silhouette measurements constrain the finite header and deck.
                var headerEdge = camera.ViewportToWorldPoint(new Vector3(.5f, 1.035f, 10f));
                var deckEdge = camera.ViewportToWorldPoint(new Vector3(.5f, -.076f, 10f));

                instance.Resize(new Vector2Int(1920, 1200));
                Assert.That(camera.WorldToViewportPoint(headerEdge).y, Is.InRange(1f, 1.01f));
                Assert.That(camera.WorldToViewportPoint(deckEdge).y, Is.InRange(-.01f, 0f));

                foreach (var size in new[] { new Vector2Int(2520, 1080), new Vector2Int(1920, 1080) })
                {
                    instance.Resize(size);
                    Assert.That(camera.WorldToViewportPoint(top).y, Is.EqualTo(1f).Within(.0001f));
                    Assert.That(camera.WorldToViewportPoint(bottom).y, Is.EqualTo(0f).Within(.0001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void Projector_SitsOnTheLens_AndIsWired()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            var projector = console.Projector;
            Assert.IsTrue(projector, "the console wires its projector");
            Assert.IsTrue(projector.transform.IsChildOf(console.transform), "the projector is nested in the console");
            Assert.That(Vector3.Distance(projector.transform.position, console.ProjectorLens.transform.position),
                Is.LessThan(0.01f));
        }

        [Test]
        public void DisplaysControlsAndRaycasterAreWired()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            var data = new SerializedObject(console);
            foreach (var name in new[]
                     {
                         "mainDisplay", "detailDisplay", "mainView", "detailView", "displayProfile",
                         "selectorKnob", "knob", "previousButton", "nextButton", "launchButton", "launchLegend"
                     })
                Assert.IsTrue(data.FindProperty(name).objectReferenceValue, name);
            Assert.IsTrue(console.GetComponent<HangarConsoleRaycaster>(), "the console raycasts its own pointers");
            Assert.That(console.GetComponentsInChildren<HangarControl>(true), Has.Length.EqualTo(4));
            Assert.That(console.GetComponentsInChildren<HangarDisplay>(true), Has.Length.EqualTo(2));
            foreach (var display in console.GetComponentsInChildren<HangarDisplay>(true))
                Assert.IsNull(display.Canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), display.name);
        }

        [Test]
        public void ControlsPressAlongTheirAuthoredUp_AwayFromTheCamera_AndTheKnobTurnsAboutIt()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            var camera = console.HardwareCamera.transform;
            foreach (var part in new[] { console.SelectorKnob, console.PreviousButton, console.NextButton, console.LaunchButton })
            {
                var axis = new SerializedObject(part.GetComponent<HangarControl>()).FindProperty("pressAxis").vector3Value;
                Assert.That(Vector3.Distance(axis, Vector3.down), Is.LessThan(.001f), part.name + " presses along its own down");
                Assert.That(Vector3.Dot(part.TransformDirection(axis), camera.forward), Is.GreaterThan(0f), part.name + " presses away from the camera");
            }
            Assert.That(Vector3.Distance(console.KnobAxis, Vector3.up), Is.LessThan(.001f));
            var grip = console.SelectorKnob.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            Assert.That(new Vector2(grip.x, grip.z).magnitude, Is.LessThan(.001f), "the knob's grip is centred on its pivot");
        }

        [Test]
        public void EachSlotHasAnArrowPair_AndTheDisplaysDriveTheirSpillLights()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            var view = console.GetComponentInChildren<HangarMainDisplayView>(true);
            foreach (HangarSlot slot in System.Enum.GetValues(typeof(HangarSlot)))
            foreach (var step in new[] { -1, 1 })
            {
                var arrow = view.Arrow(slot, step);
                Assert.IsTrue(arrow, $"{slot} {step}");
                Assert.IsTrue(arrow.targetGraphic && arrow.targetGraphic.raycastTarget, $"{slot} {step} arrow is clickable");
            }
            foreach (var display in console.GetComponentsInChildren<HangarDisplay>(true))
                Assert.IsTrue(new SerializedObject(display).FindProperty("spill").objectReferenceValue, display.name + " spill");
        }

        [Test]
        public void CameraMatchesTheApprovedLensAndPose()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            var camera = console.HardwareCamera;
            Assert.That(Vector3.Distance(camera.transform.localPosition, new Vector3(0f, 6.8f, -18f)), Is.LessThan(.0001f));
            Assert.That(Vector3.Angle(camera.transform.forward, new Vector3(0f, -.1131574f, .9935771f)), Is.LessThan(.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(34.11393f).Within(.0001f));
        }

        [Test]
        public void GlassUsesTheWholeDisplayUvRange()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            foreach (var glass in new[] { console.MainScreenGlass, console.DetailScreenGlass })
            {
                var mesh = glass.GetComponent<MeshFilter>().sharedMesh;
                var data = Mesh.AcquireReadOnlyMeshData(mesh);
                using var uv = new Unity.Collections.NativeArray<Vector2>(data[0].vertexCount, Unity.Collections.Allocator.Temp);
                data[0].GetUVs(0, uv);
                Assert.That(uv.Min(point => point.x), Is.EqualTo(0f).Within(.001f), glass.name);
                Assert.That(uv.Max(point => point.x), Is.EqualTo(1f).Within(.001f), glass.name);
                Assert.That(uv.Min(point => point.y), Is.EqualTo(0f).Within(.001f), glass.name);
                Assert.That(uv.Max(point => point.y), Is.EqualTo(1f).Within(.001f), glass.name);
                data.Dispose();
            }
        }

        [Test]
        public void GlassCarriesNoReference_AndItsDisplayIsTwiceTheReferencePicture()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath);
            foreach (var (glass, display) in new[]
                     {
                         (console.MainScreenGlass, console.MainDisplay),
                         (console.DetailScreenGlass, console.DetailDisplay)
                     })
            {
                Assert.IsNull(glass.sharedMaterial.GetTexture("_BaseMap"), glass.name);
                Assert.IsNull(glass.sharedMaterial.GetTexture("_EmissionMap"), glass.name);
                Assert.That(glass.sharedMaterial.GetFloat("_UseEmissionAlpha"), Is.EqualTo(0f), glass.name);
                var reference = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Assets/Visuals/Hangar/Console/{glass.name.Replace("Glass", "Reference")}.png");
                Assert.AreEqual(new Vector2Int(reference.width * 2, reference.height * 2), display.Resolution, glass.name);
            }
        }

        [TestCase(1920, 1080)]
        [TestCase(1920, 1200)]
        [TestCase(2520, 1080)]
        public void DisplayRootsSitOutsideTheHardwareFrustum(int width, int height)
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(PrefabPath));
            try
            {
                instance.Resize(new Vector2Int(width, height));
                var planes = GeometryUtility.CalculateFrustumPlanes(instance.HardwareCamera);
                foreach (var display in new[] { instance.MainDisplay, instance.DetailDisplay })
                {
                    var corners = new Vector3[4];
                    display.Canvas.GetComponent<RectTransform>().GetWorldCorners(corners);
                    var bounds = new Bounds(corners[0], Vector3.zero);
                    for (var i = 1; i < corners.Length; i++) bounds.Encapsulate(corners[i]);
                    Assert.IsFalse(GeometryUtility.TestPlanesAABB(planes, bounds), display.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }
    }
}
