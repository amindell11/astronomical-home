#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    [Category("Sectors")]
    public sealed class LocaleSkyPlayModeTests
    {
        private const string SkyTrianglePath = "Assets/Visuals/Environment/Sky/SkyTriangle.asset";

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

        [UnityTest, Category("RequiresGraphics")]
        public IEnumerator FlightCameraFarFromOrigin_StillDrawsTheSkyLayers()
        {
            texture = new Texture2D(4, 4, TextureFormat.RGBAHalf, false, true);
            var colors = new Color[16];
            for (var i = 0; i < colors.Length; i++) colors[i] = new Color(2, 0.25f, 0.125f, 1);
            texture.SetPixels(colors);
            texture.Apply();
            material = new Material(Shader.Find("Environment/Flat Background")) { mainTexture = texture };
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
