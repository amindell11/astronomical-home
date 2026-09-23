using Cameras.Background;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Rendering
{
    [TestFixture, Category("Camera"), Category("RequiresGraphics")]
    public sealed class ContinuousSkyBackgroundRenderTests
    {
        private const int Width = 256;
        private const int Height = 128;
        private GameObject rig;
        private Camera camera;
        private ContinuousSkyBackground background;
        private Material source;
        private Material originalSkybox;
        private Texture2D panorama;
        private Texture2D pixels;
        private RenderTexture target;
        private RenderTexture previousTarget;

        [SetUp]
        public void SetUp()
        {
            originalSkybox = RenderSettings.skybox;
            previousTarget = RenderTexture.active;
            panorama = new Texture2D(128, 64, TextureFormat.RGBAFloat, true, true)
            {
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            var colors = new Color[128 * 64];
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 128; x++)
                colors[y * 128 + x] = new Color(1 + 0.5f * Mathf.Cos((x + 0.5f) / 128 * 2 * Mathf.PI),
                    0.2f + 3 * (y + 0.5f) / 64, 0.7f, 1);
            panorama.SetPixels(colors);
            panorama.Apply();
            source = new Material(Shader.Find("Skybox/Panoramic"));
            source.SetTexture("_MainTex", panorama);
            RenderSettings.skybox = source;
            rig = new GameObject("Continuous background render test");
            rig.SetActive(false);
            camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 7;
            camera.aspect = Width / (float)Height;
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(0, 0, -50);
            background = rig.AddComponent<ContinuousSkyBackground>();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cameras/Main Camera.prefab")
                .GetComponent<ContinuousSkyBackground>();
            EditorUtility.CopySerialized(template, background);
            rig.SetActive(true);
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            target.Create();
            pixels = new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true);
        }

        [TearDown]
        public void TearDown()
        {
            RenderTexture.active = previousTarget;
            RenderSettings.skybox = originalSkybox;
            Object.DestroyImmediate(rig);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(panorama);
        }

        [Test]
        public void ZoomAndRoundTrip_PreservePatternWhileTravelMovesIt()
        {
            var start = Render(Vector2.zero, 7);
            Assert.That(Difference(start, Render(Vector2.zero, 25)), Is.LessThan(0.002f));
            Assert.That(Difference(start, Render(new Vector2(1000, 1000), 25)), Is.GreaterThan(0.02f));
            Assert.That(Difference(start, Render(Vector2.zero, 7)), Is.LessThan(0.002f));
            var parallax = new SerializedObject(background).FindProperty("parallax").floatValue;
            var stars = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Environment/Sky/StarFieldMaterial.mat");
            var far = stars.GetFloat("_ParallaxScale") * (1 + stars.GetFloat("_ParallaxFar"));
            Assert.That(parallax, Is.GreaterThan(0).And.LessThan(far * 0.025f));
        }

        [Test]
        public void VerticalJoin_AndBothRepeatPeriods_AreContinuous()
        {
            var start = Render(Vector2.zero, 7);
            var period = new Vector2(40 * Mathf.PI, 20 * Mathf.PI * 0.56f) / 0.002f;
            Assert.That(Difference(start, Render(period, 7)), Is.LessThan(0.002f));
            Assert.That(Difference(start, Render(-period, 7)), Is.LessThan(0.002f));
            var join = -0.35f * 20 * Mathf.PI / 0.002f;
            var before = Render(new Vector2(0, join - 0.1f), 7);
            var after = Render(new Vector2(0, join + 0.1f), 7);
            Assert.That(Difference(before, after), Is.LessThan(0.002f), "A wrap must not jump between source latitudes.");
        }

        [Test]
        public void SourceEdits_PreserveHdrAndLighting_AndApplyBeforeRendering()
        {
            var first = Render(Vector2.zero, 7);
            Assert.That(first[Width * Height / 2].g, Is.GreaterThan(1), "HDR must reach the camera unclipped.");
            source.SetFloat("_Exposure", 2);
            var brighter = Render(Vector2.zero, 7);
            Assert.That(brighter[Width * Height / 2].b, Is.EqualTo(SourceBlue()).Within(0.003f));
            source.SetColor("_Tint", new Color(0.25f, 0.5f, 0.5f));
            var tinted = Render(Vector2.zero, 7);
            Assert.That(tinted[Width * Height / 2].r, Is.LessThan(brighter[Width * Height / 2].r * 0.6f));
            source.SetFloat("_Rotation", 90);
            Assert.That(Difference(tinted, Render(Vector2.zero, 7)), Is.GreaterThan(0.1f));
            Assert.That(RenderSettings.skybox, Is.SameAs(source));
            foreach (var color in first)
                Assert.That(color.b, Is.GreaterThan(0.1f), "The sky must fill every pixel.");
        }

        [Test]
        public void MaterialSwap_AndDisable_KeepTheActiveEnvironment()
        {
            Render(Vector2.zero, 7);
            var replacement = new Material(source);
            try
            {
                replacement.SetFloat("_Exposure", 3);
                RenderSettings.skybox = replacement;
                var active = Render(Vector2.zero, 7);
                Assert.That(active[Width * Height / 2].b, Is.EqualTo(SourceBlue()).Within(0.003f));
                RenderSettings.skybox = source;
                var original = Render(Vector2.zero, 7);
                Assert.That(active[Width * Height / 2].g, Is.GreaterThan(original[Width * Height / 2].g));
                background.enabled = false;
                Assert.That(rig.GetComponent<Skybox>().enabled, Is.False);
                Assert.That(RenderSettings.skybox, Is.SameAs(source));
            }
            finally { Object.DestroyImmediate(replacement); }
        }

        private float SourceBlue()
        {
            background.enabled = false;
            var value = Render(Vector2.zero, 7)[Width * Height / 2].b;
            background.enabled = true;
            return value;
        }

        private Color[] Render(Vector2 position, float size)
        {
            camera.transform.position = new Vector3(position.x, position.y, -50);
            camera.orthographicSize = size;
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            return pixels.GetPixels();
        }

        private static float Difference(Color[] a, Color[] b)
        {
            var max = 0f;
            for (var i = 0; i < a.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g));
            return max;
        }
    }
}


