using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UI.Projection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode.Rendering.Projection
{
    [Category("UI")]
    [Category("RequiresGraphics")]
    public sealed class CanopyProjectionPlayModeTests
    {
        private const int Width = 32;
        private const int Height = 16;
        private readonly List<Object> owned = new();
        private Material material;
        private MaterialPropertyBlock properties;
        private Camera sceneCamera;

        [SetUp]
        public void SetUp()
        {
            material = Own(new Material(Shader.Find("Hidden/UI/CanopyProjection")));
            properties = new MaterialPropertyBlock();
            properties.SetVector("_Resolution", new Vector4(Width, Height, 0f, 0f));
            properties.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));
            properties.SetFloat("_PersonalityEnabled", 1f);
            properties.SetFloat("_AppearAge", -1f);
            properties.SetFloat("_TransitionAge", -1f);
            properties.SetFloat("_HitAge", -1f);
            properties.SetTexture("_SceneImage", Texture(Color.black));
            properties.SetTexture("_HudInk", Texture(Color.clear));
            properties.SetTexture("_PreviousInk", Texture(Color.black));
        }

        [TearDown]
        public void TearDown()
        {
            if (sceneCamera) sceneCamera.targetTexture = null;
            for (var i = owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void EveryEffectAtMaximum_LeavesAnEmptyHudSceneUnchanged_AndLightNeverDarkensIt()
        {
            var pixels = new Color[Width * Height];
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                pixels[y * Width + x] = new Color((x + 1f) / Width * .5f, (y + 1f) / Height * .6f, .1f, 1f);
            var scene = Own(new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true));
            scene.SetPixels(pixels);
            scene.Apply();
            properties.SetTexture("_SceneImage", scene);
            properties.SetVector("_Beam", new Vector4(2f, 12f, 4f, 0f));
            properties.SetVector("_Phosphor", Vector4.one);
            properties.SetVector("_Glass", new Vector4(1f, 18f, 1f, 0f));
            properties.SetFloat("_AppearAge", .12f);
            properties.SetFloat("_TransitionAge", .15f);
            properties.SetFloat("_HitAge", .08f);
            properties.SetFloat("_Clock", 12.3f);
            var empty = Render(1);
            for (var i = 0; i < pixels.Length; i++) AssertColor(pixels[i], empty[i]);

            properties.SetTexture("_PreviousInk", Texture(new Color(.2f, .3f, .1f, 1f)));
            var lit = Render(1);
            var brighter = false;
            for (var i = 0; i < pixels.Length; i++)
            {
                Assert.That(lit[i].r, Is.GreaterThanOrEqualTo(pixels[i].r - .001f));
                Assert.That(lit[i].g, Is.GreaterThanOrEqualTo(pixels[i].g - .001f));
                Assert.That(lit[i].b, Is.GreaterThanOrEqualTo(pixels[i].b - .001f));
                brighter |= lit[i].r > pixels[i].r + .01f;
            }
            Assert.That(brighter, Is.True);
        }

        [Test]
        public void GlassWarpKeepsTheCenterStill_AndBendsBothSidesSymmetrically()
        {
            var ink = Own(new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true));
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                ink.SetPixel(x, y, new Color((x + .5f) / Width * .5f, (y + .5f) / Height * .5f, .1f));
            ink.Apply();
            properties.SetTexture("_PreviousInk", ink);
            var baseline = Render(1);
            properties.SetVector("_Glass", new Vector4(.96f, 0f, 0f, 0f));
            var warped = Render(1);
            AssertColor(baseline[Height / 2 * Width + Width / 2], warped[Height / 2 * Width + Width / 2]);
            var left = Height / 2 * Width + 3;
            var right = Height / 2 * Width + Width - 4;
            var leftShift = Mathf.LinearToGammaSpace(warped[left].r) - Mathf.LinearToGammaSpace(baseline[left].r);
            var rightShift = Mathf.LinearToGammaSpace(warped[right].r) - Mathf.LinearToGammaSpace(baseline[right].r);
            Assert.That(leftShift, Is.LessThan(-.001f));
            Assert.That(rightShift, Is.GreaterThan(.001f));
            Assert.That(leftShift + rightShift, Is.EqualTo(0f).Within(.0001f));
        }

        [Test]
        public void BloomUsesTheAlphaMask_IsolatedTextAddsLightWithoutAHalo()
        {
            properties.SetTexture("_PreviousInk", Texture(new Color(.15f, .15f, .15f, 1f)));
            properties.SetTexture("_HudInk", Texture(new Color(.04f, .04f, .04f, 0f)));
            var noBloom = Render(1);
            properties.SetVector("_Beam", new Vector4(2f, 0f, 0f, 0f));
            var text = Render(1);
            for (var i = 0; i < text.Length; i++) AssertColor(noBloom[i], text[i]);
            Assert.That(text[0].r, Is.GreaterThan(.01f));

            properties.SetTexture("_HudInk", Texture(new Color(.04f, .04f, .04f, 1f)));
            var shapes = Render(1);
            Assert.That(shapes[Width * Height / 2].r, Is.GreaterThan(text[Width * Height / 2].r + .05f));
        }

        [Test]
        public void HistoryKeepsTheBrighterCurrentOrDecayedChannel_AndZeroDecayClearsIt()
        {
            properties.SetTexture("_PreviousInk", Texture(new Color(.6f, .1f, .8f, 1f)));
            properties.SetTexture("_HudInk", Texture(new Color(0f, .25f, 0f, 1f)));
            properties.SetFloat("_Decay", .5f);
            var held = Render(0);
            AssertColor(new Color(.3f, Mathf.LinearToGammaSpace(.25f), .4f, 1f), held[0]);
            properties.SetFloat("_Decay", 0f);
            var cleared = Render(0);
            AssertColor(new Color(0f, Mathf.LinearToGammaSpace(.25f), 0f, 1f), cleared[0]);
        }

        [Test]
        public void TextShaderAddsRgbAndPreservesDestinationAlpha()
        {
            var output = DrawText(new Color(0f, 0f, 0f, .37f), Color.white);
            var pixel = Read(output)[Width * Height / 2];
            Assert.That(pixel.r, Is.GreaterThan(.9f));
            Assert.That(pixel.a, Is.EqualTo(.37f).Within(.001f));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator CameraPairRendersThroughTheFeature_AndResizesItsTarget()
        {
            sceneCamera = Own(new GameObject("Projection scene camera")).AddComponent<Camera>();
            sceneCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var sceneTarget = Own(new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear));
            sceneTarget.Create();
            sceneCamera.targetTexture = sceneTarget;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(.02f, .03f, .04f, 1f);
            sceneCamera.cullingMask = 0;
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 9f;
            sceneCamera.transform.position = new Vector3(3f, 4f, -12f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cameras/UI Cam.prefab");
            var projection = Own(Object.Instantiate(prefab)).GetComponent<CanopyProjection>();
            projection.Initialize(sceneCamera);
            var canvas = Own(new GameObject("Projection test canvas", typeof(RectTransform), typeof(Canvas)))
                .GetComponent<Canvas>();
            canvas.gameObject.layer = 10;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = projection.Camera;
            canvas.planeDistance = 100f;
            var image = Own(new GameObject("Projection test ink", typeof(RectTransform), typeof(Image)))
                .GetComponent<Image>();
            image.gameObject.layer = 10;
            image.transform.SetParent(canvas.transform, false);
            image.rectTransform.anchorMin = Vector2.one * .25f;
            image.rectTransform.anchorMax = Vector2.one * .75f;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.color = new Color(.3f, .1f, .05f, 1f);
            Canvas.ForceUpdateCanvases();
            var canvasRect = (RectTransform)canvas.transform;
            Assert.That(canvasRect.rect.height * canvasRect.lossyScale.y,
                Is.EqualTo(2f * sceneCamera.orthographicSize).Within(.01f));
            var original = projection.Camera.targetTexture;
            Assert.That(original.IsCreated(), Is.True);
            Assert.That(original.width, Is.EqualTo(Width));
            Assert.That(original.height, Is.EqualTo(Height));
            Assert.That(original.depth, Is.GreaterThanOrEqualTo(24));
            Assert.That(projection.Camera.transform.position, Is.EqualTo(sceneCamera.transform.position));
            Assert.That(projection.Camera.orthographicSize, Is.EqualTo(9f));
            Assert.That(projection.Camera.depth, Is.LessThan(sceneCamera.depth));
            yield return null;
            yield return null;
            var capturedInk = Read(projection.Camera.targetTexture);
            Assert.That(capturedInk[0].a, Is.EqualTo(0f).Within(.001f));
            Assert.That(capturedInk[Height / 2 * Width + Width / 2].a, Is.GreaterThan(.5f));
            var composite = Read(sceneTarget);
            Assert.That(composite[Height / 2 * Width + Width / 2].r, Is.GreaterThan(composite[0].r + .02f));

            image.color = Color.clear;
            yield return new WaitForSecondsRealtime(3f);
            var faded = Read(sceneTarget);
            Assert.That(faded[Height / 2 * Width + Width / 2].r - faded[0].r, Is.LessThan(.0005f),
                "Removed ink must decay below visible precision instead of leaving a permanent trail.");

            var resized = Own(new RenderTexture(Width * 2, Height * 2, 24));
            resized.Create();
            sceneCamera.targetTexture = resized;
            yield return null;
            yield return null;
            Assert.That(projection.Camera.targetTexture.width, Is.EqualTo(Width * 2));
            Assert.That(projection.Camera.targetTexture.height, Is.EqualTo(Height * 2));
            Assert.That(projection.Camera.targetTexture, Is.Not.SameAs(original));
            projection.enabled = false;
            Assert.That(projection.Camera.targetTexture, Is.Null);
            Assert.That(projection.Camera.enabled, Is.False);
        }
#endif

        private RenderTexture DrawText(Color backing, Color ink)
        {
            var text = Own(new Material(Shader.Find("UI/CanopyText")));
            text.SetColor("_Color", ink);
            var mesh = Own(new Mesh
            {
                vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            });
            var output = Target();
            using (var commands = new CommandBuffer())
            {
                commands.SetRenderTarget(output);
                commands.ClearRenderTarget(false, true, backing);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                commands.DrawMesh(mesh, Matrix4x4.identity, text);
                Graphics.ExecuteCommandBuffer(commands);
            }
            return output;
        }

        private T Own<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }

        private Texture2D Texture(Color color)
        {
            var texture = Own(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private RenderTexture Target()
        {
            var target = Own(new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
            target.Create();
            return target;
        }

        private Color[] Render(int pass)
        {
            var output = Target();
            using (var commands = new CommandBuffer())
            {
                commands.SetRenderTarget(output);
                commands.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, properties);
                Graphics.ExecuteCommandBuffer(commands);
            }
            return Read(output);
        }

        private Color[] Read(RenderTexture output)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = output;
            try
            {
                var image = Own(new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true));
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image.GetPixels();
            }
            finally { RenderTexture.active = previous; }
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.001f));
        }
    }
}
