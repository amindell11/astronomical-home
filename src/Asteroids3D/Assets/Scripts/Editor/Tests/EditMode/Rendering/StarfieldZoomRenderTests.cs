using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering
{
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresGraphics")]
    public class StarfieldZoomRenderTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(1f, 1f)]
        [TestCase(0.5f, 0.5f)]
        public void ZoomResponses_IndependentlyControlSizeAndSpacing(float sizeResponse, float spacingResponse)
        {
            const int resolution = 1024;
            var material = new Material(Shader.Find("Custom/StarField"));
            material.SetFloat("_SizeZoomResponse", sizeResponse);
            material.SetFloat("_SpacingZoomResponse", spacingResponse);
            material.SetFloat("_StarDensity", 1);
            material.SetFloat("_NearLayerShare", 1);
            material.SetFloat("_CellScale", 1);
            material.SetFloat("_StarSizeMin", 0.12f);
            material.SetFloat("_StarSizeMax", 0.12f);
            material.SetFloat("_PositionJitter", 0);
            material.SetFloat("_ParallaxNear", 0);
            material.SetFloat("_ParallaxFar", 0);
            material.SetFloat("_HaloStrength", 0);
            material.SetFloat("_TwinkleAmount", 0);
            material.SetColor("_ColorCool", Color.white);
            material.SetColor("_ColorWarm", Color.white);
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-100, -100, 0), new Vector3(100, -100, 0),
                    new Vector3(100, 100, 0), new Vector3(-100, 100, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            var target = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false, true);
            var commands = new CommandBuffer();
            var previousTarget = RenderTexture.active;
            var previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            try
            {
                target.Create();
                var baseline = Render(7);
                var zoomed = Render(3.5f);
                var radiusRatio = Radius(zoomed) / Radius(baseline);
                Assert.That(radiusRatio, Is.EqualTo(Mathf.Pow(2, sizeResponse)).Within(0.2f),
                    "Star size must respond independently of spacing, including antialiasing.");
                Assert.That(PeakDistance(zoomed, Mathf.Pow(2, spacingResponse)) /
                    PeakDistance(baseline, 1), Is.EqualTo(Mathf.Pow(2, spacingResponse)).Within(0.06f),
                    "Star spacing must respond independently of size around the camera center.");
                if (sizeResponse == 0 && spacingResponse == 0)
                {
                    var maximumDifference = 0f;
                    for (var i = 0; i < baseline.Length; i++)
                        maximumDifference = Mathf.Max(maximumDifference, Mathf.Abs(baseline[i].r - zoomed[i].r));
                    Assert.Less(maximumDifference, 0.005f, "Both zero responses must preserve the entire pattern.");
                }
            }
            finally
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

            Color[] Render(float halfHeight)
            {
                var cameraPosition = new Vector3(13.5f, -7.5f, -10);
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
                pixels.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                pixels.Apply();
                return pixels.GetPixels();
            }

            float Radius(Color[] colors)
            {
                var light = 0f;
                var moment = 0f;
                for (var y = resolution / 2 - 32; y < resolution / 2 + 32; y++)
                for (var x = resolution / 2 - 32; x < resolution / 2 + 32; x++)
                {
                    var intensity = colors[y * resolution + x].r;
                    light += intensity;
                    moment += intensity * ((x - (resolution - 1) * 0.5f) * (x - (resolution - 1) * 0.5f) + (y - (resolution - 1) * 0.5f) * (y - (resolution - 1) * 0.5f));
                }
                Assert.Greater(light, 0.01f, "The central star must be visible.");
                return Mathf.Sqrt(moment / light);
            }

            float PeakDistance(Color[] colors, float spacingScale)
            {
                var expected = (resolution - 1) * 0.5f + resolution / 14f * spacingScale;
                var brightest = 0f;
                var peak = 0;
                for (var x = Mathf.FloorToInt(expected) - 5; x <= Mathf.CeilToInt(expected) + 5; x++)
                {
                    var intensity = colors[(resolution / 2 - 1) * resolution + x].r;
                    if (intensity <= brightest) continue;
                    brightest = intensity;
                    peak = x;
                }
                Assert.Greater(brightest, 0.01f, "The neighboring star must be visible at the expected spacing.");
                return peak - (resolution - 1) * 0.5f;
            }
        }
    }
}
