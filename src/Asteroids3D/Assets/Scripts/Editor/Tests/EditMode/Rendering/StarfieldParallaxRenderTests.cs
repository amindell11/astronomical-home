using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering
{
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresGraphics")]
    public class StarfieldParallaxRenderTests
    {
        private const int Resolution = 1024;
        private Material material;
        private Mesh mesh;
        private RenderTexture target;
        private Texture2D pixels;
        private CommandBuffer commands;
        private RenderTexture previousTarget;
        private Vector4 previousCamera;

        [SetUp]
        public void SetUp()
        {
            material = new Material(Shader.Find("Custom/StarField"));
            material.SetFloat("_Seed", 2);
            material.SetFloat("_StarDensity", 1);
            material.SetFloat("_NearLayerShare", 1);
            material.SetFloat("_CellScale", 1);
            material.SetFloat("_StarSizeMin", 0.025f);
            material.SetFloat("_StarSizeMax", 0.025f);
            material.SetFloat("_PositionJitter", 0);
            material.SetFloat("_SizeZoomResponse", 0);
            material.SetFloat("_HaloStrength", 0);
            material.SetFloat("_TwinkleAmount", 0);
            material.SetColor("_ColorCool", Color.white);
            material.SetColor("_ColorWarm", Color.white);
            mesh = new Mesh
            {
                vertices = new[] { new Vector3(-100, -100, 0), new Vector3(100, -100, 0),
                    new Vector3(100, 100, 0), new Vector3(-100, 100, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            target = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            pixels = new Texture2D(Resolution, Resolution, TextureFormat.RGBAFloat, false, true);
            commands = new CommandBuffer();
            previousTarget = RenderTexture.active;
            previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            target.Create();
        }

        [TearDown]
        public void TearDown()
        {
            RenderTexture.active = previousTarget;
            Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
            commands.Release();
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(0.5f, 0f, 1f)]
        [TestCase(1f, 0f, 1f)]
        [TestCase(2f, 0f, 1f)]
        [TestCase(0f, 0.6f, 1f)]
        [TestCase(0.5f, 0.6f, 0f)]
        [TestCase(1f, 0.6f, 0f)]
        [TestCase(2f, 0.6f, 0f)]
        public void CameraTravel_UsesOverallScaleAndGeometricZoom(
            float overallScale, float parallax, float nearShare)
        {
            material.SetFloat("_NearLayerShare", nearShare);
            material.SetFloat("_ParallaxNear", parallax);
            material.SetFloat("_ParallaxFar", parallax);
            material.SetFloat("_ParallaxScale", overallScale);
            var movementScale = (1 + parallax) * overallScale;
            var start = movementScale > 0 ? Vector2.one * (0.5f / movementScale) : Vector2.zero;
            foreach (var response in new[] { 0f, 0.5f, 1f })
            foreach (var halfHeight in new[] { 14f, 28f })
            {
                material.SetFloat("_SpacingZoomResponse", response);
                var pixelsPerUnit = Resolution / 14f * Mathf.Pow(7 / halfHeight, response);
                var starCenter = Vector2.one * ((Resolution - 1) * 0.5f +
                    (0.5f - start.x * movementScale) * pixelsPerUnit);
                var before = Center(Render(start, halfHeight), starCenter);
                var after = Center(Render(start + Vector2.right * 0.02f, halfHeight), starCenter);
                var expected = -0.02f * movementScale * pixelsPerUnit;
                Assert.That(after - before, Is.EqualTo(expected).Within(0.15f),
                    $"Spacing response {response}, half-height {halfHeight}: movement must follow magnification.");
            }
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void CombinedPanAndZoom_ReturnsToTheSamePattern(float spacingResponse)
        {
            material.SetFloat("_SpacingZoomResponse", spacingResponse);
            material.SetFloat("_ParallaxScale", 0.7f);
            material.SetFloat("_NearLayerShare", 0.5f);
            var startPosition = new Vector2(13.5f, -7.5f);
            var endPosition = new Vector2(-4.25f, 6.75f);
            var start = Render(startPosition, 28);
            var end = Render(endPosition, 7);
            Assert.Greater(Difference(start, end), 0.1f, "The transition must visibly change the starfield.");
            foreach (var steps in new[] { 1, 2, 30 })
            {
                for (var i = 1; i <= steps; i++)
                {
                    var t = i / (float)steps;
                    Render(Vector2.Lerp(startPosition, endPosition, t), Mathf.Lerp(28, 7, t));
                }
                Assert.That(Difference(end, Render(endPosition, 7)), Is.LessThan(0.0001f),
                    "The destination pattern must not depend on the number of transition frames.");
                for (var i = 1; i <= steps; i++)
                {
                    var t = i / (float)steps;
                    Render(Vector2.Lerp(endPosition, startPosition, t), Mathf.Lerp(7, 28, t));
                }
                Assert.That(Difference(start, Render(startPosition, 28)), Is.LessThan(0.0001f),
                    "A camera round trip must not leave residual starfield drift.");
            }
        }

        private Color[] Render(Vector2 position, float halfHeight)
        {
            var cameraPosition = new Vector3(position.x, position.y, -10);
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true, true, Color.black);
            commands.SetViewProjectionMatrices(
                Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.Translate(-cameraPosition),
                GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-halfHeight, halfHeight,
                    -halfHeight, halfHeight, 0.1f, 100), true));
            commands.SetGlobalVector("_WorldSpaceCameraPos", cameraPosition);
            commands.DrawMesh(mesh, Matrix4x4.identity, material);
            Graphics.ExecuteCommandBuffer(commands);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            pixels.Apply();
            return pixels.GetPixels();
        }

        private static float Center(Color[] colors, Vector2 center)
        {
            var light = 0f;
            var moment = 0f;
            for (var y = Mathf.RoundToInt(center.y) - 10; y < Mathf.RoundToInt(center.y) + 10; y++)
            for (var x = Mathf.RoundToInt(center.x) - 10; x < Mathf.RoundToInt(center.x) + 10; x++)
            {
                var intensity = colors[y * Resolution + x].r;
                light += intensity;
                moment += x * intensity;
            }
            Assert.Greater(light, 0.01f, "The tracked star must remain visible.");
            return moment / light;
        }

        private static float Difference(Color[] a, Color[] b)
        {
            var maximum = 0f;
            for (var i = 0; i < a.Length; i++)
                maximum = Mathf.Max(maximum, Mathf.Abs(a[i].r - b[i].r));
            return maximum;
        }
    }
}
