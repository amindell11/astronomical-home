#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Locales;
using Tests.PlayMode.Common.Locales;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    [Category("Sectors")]
    public sealed class LocaleSkyPlayModeTests
    {
        private const string SkyTrianglePath = "Assets/Visuals/Locales/Sky/SkyTriangle.asset";

        private readonly List<GameObject> roots = new();
        private GameObject cameraRoot;
        private Texture2D texture;
        private Material material;
        private RenderTexture target;
        private LightingSnapshot bootLighting;

        [SetUp]
        public void SetUp()
        {
            Assert.That(LayerIds.Sky, Is.GreaterThanOrEqualTo(0), "test premise: the Sky layer exists");
            bootLighting = LightingSnapshot.Capture();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in roots) Object.Destroy(root);
            roots.Clear();
            if (cameraRoot) Object.Destroy(cameraRoot);
            if (material) Object.Destroy(material);
            if (texture) Object.Destroy(texture);
            if (target) { target.Release(); Object.Destroy(target); }
            bootLighting.Restore();
        }

        [UnityTest]
        public IEnumerator SetVisible_EnablesOnlyTheShownLocalesLayers()
        {
            var a = BuildLocale(null, out var aLayer);
            var b = BuildLocale(null, out var bLayer);
            Wake(a, b);
            a.SetVisible(true);
            b.SetVisible(false);
            yield return null;
            Assert.IsTrue(aLayer.enabled, "the shown locale's sky layers draw");
            Assert.IsFalse(bLayer.enabled, "a loaded locale that is not shown keeps its sky layers off");

            a.SetVisible(false);
            b.SetVisible(true);
            yield return null;
            Assert.IsFalse(aLayer.enabled, "the previously shown locale's sky layers turn off");
            Assert.IsTrue(bLayer.enabled, "the newly shown locale's sky layers turn on");
        }

        [UnityTest]
        public IEnumerator SetVisible_EnablesOnlyTheShownLocalesVolumeAndLight()
        {
            var a = BuildLocale(null, out _);
            var b = BuildLocale(null, out _);
            var aVolume = AddFollower<Volume>(a);
            var bVolume = AddFollower<Volume>(b);
            var aLight = AddFollower<Light>(a);
            var bLight = AddFollower<Light>(b);
            Wake(a, b);
            a.SetVisible(true);
            b.SetVisible(false);
            yield return null;
            Assert.IsTrue(aVolume.enabled && aLight.enabled, "the shown locale grades and lights the world");
            Assert.IsFalse(bVolume.enabled || bLight.enabled, "a preloaded locale's volume and light stay off");

            a.SetVisible(false);
            b.SetVisible(true);
            yield return null;
            Assert.IsFalse(aVolume.enabled || aLight.enabled, "the previously shown locale stops grading and lighting");
            Assert.IsTrue(bVolume.enabled && bLight.enabled, "the newly shown locale grades and lights the world");
        }

        [UnityTest, Category("RequiresGraphics")]
        public IEnumerator BackdropRender_UsesEachImagesOwnVolumeAndLighting_ThenRestoresTheShownLocale()
        {
            var a = BuildLocale(null, out _, new Color(0.3f, 0.1f, 0.1f));
            var b = BuildLocale(null, out _, new Color(0.1f, 0.3f, 0.1f));
            var aVolume = AddFollower<Volume>(a);
            var bVolume = AddFollower<Volume>(b);
            Wake(a, b);
            a.SetVisible(true);
            a.ApplyLighting();
            b.SetVisible(false);
            cameraRoot = new GameObject("Backdrop camera");
            var camera = cameraRoot.AddComponent<Camera>();
            camera.enabled = false;
            target = new RenderTexture(16, 16, 24);
            target.Create();
            var pairRoot = new GameObject("Locale backdrop pair");
            pairRoot.transform.SetParent(cameraRoot.transform);
            var pair = pairRoot.AddComponent<LocaleBackdropPair>();
            pair.Initialize(a, b, () => { });
            var rendered = new List<string>();
            void Record(ScriptableRenderContext _, Camera current)
            {
                if (current == camera)
                    rendered.Add($"first {aVolume.enabled}, second {bVolume.enabled}, ambient {RenderSettings.ambientLight}");
            }
            var arrival = RenderTexture.GetTemporary(target.descriptor);
            RenderPipelineManager.beginCameraRendering += Record;
            try { pair.Render(camera, target, arrival); }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Record;
                RenderTexture.ReleaseTemporary(arrival);
            }
            Assert.That(rendered, Is.EqualTo(new[]
            {
                $"first True, second False, ambient {new Color(0.3f, 0.1f, 0.1f)}",
                $"first False, second True, ambient {new Color(0.1f, 0.3f, 0.1f)}"
            }), "each travel image renders under its own locale's volume and lighting");
            Assert.IsTrue(aVolume.enabled && !bVolume.enabled, "the shown locale alone grades the world after the backdrop renders");
            Assert.AreEqual(new Color(0.3f, 0.1f, 0.1f), RenderSettings.ambientLight, "the shown locale's lighting returns");
            yield return null;
        }

        [UnityTest, Category("RequiresGraphics")]
        public IEnumerator FlightCameraFarFromOrigin_DrawsSkyInBothSharedLocaleTravelImages()
        {
            texture = new Texture2D(4, 4, TextureFormat.RGBAHalf, false, true);
            var colors = new Color[16];
            for (var i = 0; i < colors.Length; i++) colors[i] = new Color(2, 0.25f, 0.125f, 1);
            texture.SetPixels(colors);
            texture.Apply();
            material = new Material(Shader.Find("Locales/Flat Background")) { mainTexture = texture };
            var sky = BuildLocale(material, out _);
            Wake(sky);
            sky.SetVisible(true);

            cameraRoot = new GameObject("Flight camera");
            var camera = cameraRoot.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7;
            camera.cullingMask = 1 << LayerIds.Sky;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBHalf);
            target.Create();
            camera.targetTexture = target;

            foreach (var position in new[] { new Vector3(800000, -800000, -10), new Vector3(-800000, 800000, -10) })
            {
                cameraRoot.transform.position = position;
                yield return null;
                yield return null;
                Assert.That(ReadCenter().r, Is.GreaterThan(1.5f), $"the sky layer must still draw with the camera at {position}");
            }

            var pairRoot = new GameObject("Shared locale backdrop");
            pairRoot.transform.SetParent(cameraRoot.transform);
            var pair = pairRoot.AddComponent<LocaleBackdropPair>();
            pair.Initialize(sky, sky, () => { });
            var arrival = RenderTexture.GetTemporary(target.descriptor);
            try
            {
                pair.Render(camera, target, arrival);
                Assert.That(ReadCenter().r, Is.GreaterThan(1.5f), "Shared departure still renders its sky.");
                Graphics.Blit(arrival, target);
                Assert.That(ReadCenter().r, Is.GreaterThan(1.5f), "Shared arrival renders the same sky.");
            }
            finally { RenderTexture.ReleaseTemporary(arrival); }
        }

        private LocaleSky BuildLocale(Material layerMaterial, out MeshRenderer layer, Color ambient = default)
        {
            var root = new GameObject("Locale");
            root.SetActive(false);
            roots.Add(root);
            var layerObject = new GameObject("Layer") { layer = LayerIds.Sky };
            layerObject.transform.SetParent(root.transform, false);
            layerObject.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SkyTrianglePath);
            layer = layerObject.AddComponent<MeshRenderer>();
            layer.sharedMaterial = layerMaterial;
            var sky = root.AddComponent<LocaleSky>();
            var lighting = new SerializedObject(sky);
            lighting.FindProperty("lighting.ambientMode").intValue = (int)AmbientMode.Flat;
            lighting.FindProperty("lighting.ambientSkyColor").colorValue = ambient;
            lighting.ApplyModifiedPropertiesWithoutUndo();
            return sky;
        }

        private static void Wake(params LocaleSky[] skies)
        {
            foreach (var sky in skies) sky.gameObject.SetActive(true);
        }

        // A locale finds its followers once, when it wakes, so they join while it is still inactive.
        private static T AddFollower<T>(LocaleSky sky) where T : Behaviour
        {
            var follower = new GameObject(typeof(T).Name);
            follower.transform.SetParent(sky.transform, false);
            var component = follower.AddComponent<T>();
            var volume = component as Volume;
            if (volume) volume.isGlobal = true;
            return component;
        }

        private Color ReadCenter()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            readback.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
            readback.Apply();
            var result = readback.GetPixel(0, 0);
            Object.Destroy(readback);
            RenderTexture.active = previous;
            return result;
        }
    }
}
#endif
