#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    [Category("Sectors")]
    public sealed class LocaleSkyPlayModeTests
    {
        private const string SkyTrianglePath = "Assets/Visuals/Locales/Sky/SkyTriangle.asset";

        private Scene original;
        private Scene first;
        private Scene second;
        private GameObject cameraRoot;
        private Texture2D texture;
        private Material material;
        private RenderTexture target;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(LayerIds.Sky, Is.GreaterThanOrEqualTo(0), "test premise: the Sky layer exists");
            original = SceneManager.GetActiveScene();
            first = SceneManager.CreateScene("Locale A");
            second = SceneManager.CreateScene("Locale B");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (cameraRoot) Object.Destroy(cameraRoot);
            if (material) Object.Destroy(material);
            if (texture) Object.Destroy(texture);
            if (target) { target.Release(); Object.Destroy(target); }
            SceneManager.SetActiveScene(original);
            yield return SceneManager.UnloadSceneAsync(first);
            yield return SceneManager.UnloadSceneAsync(second);
        }

        [UnityTest]
        public IEnumerator ActiveSceneSwap_EnablesOnlyTheActiveRootsLayers()
        {
            SceneManager.SetActiveScene(first);
            var a = BuildRoot(first, null);
            var b = BuildRoot(second, null);
            yield return null;
            Assert.IsTrue(a.enabled, "the active locale's sky layers draw");
            Assert.IsFalse(b.enabled, "an inactive loaded locale's sky layers stay off");

            SceneManager.SetActiveScene(second);
            yield return null;
            Assert.IsFalse(a.enabled, "the previously active locale's sky layers turn off");
            Assert.IsTrue(b.enabled, "the newly active locale's sky layers turn on");
        }

        [UnityTest]
        public IEnumerator ActiveSceneSwap_EnablesOnlyTheActiveLocalesVolume()
        {
            SceneManager.SetActiveScene(first);
            var a = AddVolume(first);
            var b = AddVolume(second);
            BuildRoot(first, null);
            BuildRoot(second, null);
            yield return null;
            Assert.IsTrue(a.enabled, "the active locale grades the world");
            Assert.IsFalse(b.enabled, "a preloaded locale's volume stays off");

            SceneManager.SetActiveScene(second);
            yield return null;
            Assert.IsFalse(a.enabled, "the previously active locale stops grading");
            Assert.IsTrue(b.enabled, "the newly active locale grades the world");
        }

        [UnityTest, Category("RequiresGraphics")]
        public IEnumerator BackdropRender_EnablesEachImagesOwnVolume_ThenRestoresTheActiveOne()
        {
            SceneManager.SetActiveScene(first);
            var a = AddVolume(first);
            var b = AddVolume(second);
            BuildRoot(first, null);
            BuildRoot(second, null);
            cameraRoot = new GameObject("Backdrop camera");
            var camera = cameraRoot.AddComponent<Camera>();
            camera.enabled = false;
            target = new RenderTexture(16, 16, 24);
            target.Create();
            var pairRoot = new GameObject("Locale backdrop pair");
            pairRoot.transform.SetParent(cameraRoot.transform);
            var pair = pairRoot.AddComponent<LocaleBackdropPair>();
            pair.Initialize(first, second, () => { });
            var rendered = new List<string>();
            void Record(ScriptableRenderContext _, Camera current)
            {
                if (current == camera) rendered.Add($"first {a.enabled}, second {b.enabled}");
            }
            var arrival = RenderTexture.GetTemporary(target.descriptor);
            RenderPipelineManager.beginCameraRendering += Record;
            try { pair.Render(camera, target, arrival); }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Record;
                RenderTexture.ReleaseTemporary(arrival);
            }
            Assert.That(rendered, Is.EqualTo(new[] { "first True, second False", "first False, second True" }),
                "each travel image renders under its own locale's volume");
            Assert.IsTrue(a.enabled && !b.enabled, "the active locale alone grades the world after the backdrop renders");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BackdropToASkylessLocale_FailsWithoutHoldingAHalfBuiltPair()
        {
            SceneManager.SetActiveScene(first);
            BuildRoot(first, null);
            var locales = new LocaleService();
            yield return locales.ApplyLocaleAsync(first.name);
            cameraRoot = new GameObject("Backdrop parent");
            Assert.That(() => locales.PrepareBackdrop(second.name, cameraRoot.transform, _ => { }).MoveNext(),
                Throws.InvalidOperationException.With.Message.Contains("no LocaleSky"));
            yield return null;
            Assert.That(cameraRoot.transform.childCount, Is.Zero, "the failed pair is destroyed");

            BuildRoot(second, null);
            LocaleBackdropPair pair = null;
            yield return locales.PrepareBackdrop(second.name, cameraRoot.transform, built => pair = built);
            Assert.That(pair, Is.Not.Null, "the next backdrop is not refused as already held");
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
            SceneManager.SetActiveScene(first);
            BuildRoot(first, material);

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
            pair.Initialize(first, first, () => { });
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

        private static MeshRenderer BuildRoot(Scene scene, Material layerMaterial)
        {
            var root = new GameObject("Sky");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var layer = new GameObject("Layer") { layer = LayerIds.Sky };
            layer.transform.SetParent(root.transform, false);
            layer.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SkyTrianglePath);
            var renderer = layer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = layerMaterial;
            root.AddComponent<LocaleSky>();
            root.SetActive(true);
            return renderer;
        }

        private static Volume AddVolume(Scene scene)
        {
            var atmosphere = new GameObject("Atmosphere");
            SceneManager.MoveGameObjectToScene(atmosphere, scene);
            var volume = atmosphere.AddComponent<Volume>();
            volume.isGlobal = true;
            return volume;
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
