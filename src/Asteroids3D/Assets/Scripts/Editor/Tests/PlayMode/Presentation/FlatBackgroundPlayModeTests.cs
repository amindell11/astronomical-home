#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Substrate;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    [Category("Sectors")]
    public sealed class FlatBackgroundPlayModeTests
    {
        private const string SkyTrianglePath = "Assets/Visuals/Locales/Sky/SkyTriangle.asset";
        // A power of two keeps whole-repeat camera positions and the shader's division exact.
        private const float RepeatDistance = 65536;

        private GameObject cameraRoot;
        private GameObject layer;
        private Texture2D texture;
        private Material background;
        private RenderTexture target;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(LayerIds.Sky, Is.GreaterThanOrEqualTo(0), "test premise: the Sky layer exists");
            texture = new Texture2D(16, 16, TextureFormat.RGBAHalf, false, true) { wrapMode = TextureWrapMode.Repeat };
            var colors = new Color[256];
            for (var i = 0; i < colors.Length; i++) colors[i] = new Color(2 + i % 16 * 0.125f, 0.25f, i / 16 * 0.0625f, 1);
            texture.SetPixels(colors);
            texture.Apply();
            background = new Material(Shader.Find("Locales/Flat Background")) { mainTexture = texture };
            background.SetFloat("_RepeatDistance", RepeatDistance);
            background.SetFloat("_ViewHeightInTiles", 0.5f);
            layer = new GameObject("Flat background") { layer = LayerIds.Sky };
            layer.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SkyTrianglePath);
            layer.AddComponent<MeshRenderer>().sharedMaterial = background;

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
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (cameraRoot) Object.Destroy(cameraRoot);
            if (layer) Object.Destroy(layer);
            if (background) Object.Destroy(background);
            if (texture) Object.Destroy(texture);
            if (target) { target.Release(); Object.Destroy(target); }
            yield return null;
        }

        [UnityTest, Category("RequiresGraphics")]
        public IEnumerator HomeAndWholeRepeatsAway_RenderIdentically_InHdr()
        {
            var home = new Vector3(128, 384, -10);
            yield return RenderAt(home);
            var baseline = ReadAll();
            Assert.That(baseline[32 * 64 + 32].r, Is.GreaterThan(1.5f), "Native flat clouds must reach the camera as HDR.");

            foreach (var repeats in new[] { 1, -1, 12, -12 })
            {
                yield return RenderAt(home + new Vector3(repeats, -repeats, 0) * RepeatDistance);
                CollectionAssert.AreEqual(baseline, ReadAll(), $"{repeats} whole repeats away must render as home.");
            }

            yield return RenderAt(home + new Vector3(RepeatDistance / 2, 0, 0));
            CollectionAssert.AreNotEqual(baseline, ReadAll(), "Travel inside a repeat must reveal different clouds.");
        }

        private IEnumerator RenderAt(Vector3 position)
        {
            cameraRoot.transform.position = position;
            yield return null;
            yield return null;
        }

        private Color[] ReadAll()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var readback = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply();
            var result = readback.GetPixels();
            Object.Destroy(readback);
            RenderTexture.active = previous;
            return result;
        }
    }
}
#endif
