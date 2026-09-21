using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering
{
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresGraphics")]
    public class StarfieldHaloRenderTests
    {
        [TestCase(0.08f, 2f, 0.5f, 0.2f, 1f)]
        [TestCase(0.2f, 4f, 0.6f, 1f, 1f)]
        [TestCase(0.2f, 4f, 0.6f, 1f, 0f)]
        public void AnimatedHalos_AreContinuousAcrossCellBorders(
            float radius, float haloSize, float jitter, float twinkle, float nearShare)
        {
            const int width = 8;
            const int height = 512;
            const float halfStripWidth = 0.0004f;
            var source = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Visuals/Environment/Sky/StarFieldMaterial.mat");
            Assert.IsNotNull(source);
            var material = new Material(source);
            material.SetFloat("_Seed", 0);
            material.SetFloat("_StarDensity", 1);
            material.SetFloat("_CellScale", 1);
            material.SetFloat("_StarSizeMin", radius);
            material.SetFloat("_StarSizeMax", radius);
            material.SetFloat("_HaloSize", haloSize);
            material.SetFloat("_HaloStrength", 1);
            material.SetFloat("_ShapeVariation", 1);
            material.SetFloat("_DepthElongation", 1);
            material.SetFloat("_Softness", 1);
            material.SetFloat("_TwinkleNoise", 1);
            material.SetFloat("_PositionJitter", jitter);
            material.SetFloat("_TwinkleAmount", twinkle);
            material.SetFloat("_TwinkleDurationMin", 4);
            material.SetFloat("_TwinkleDurationMax", 4);
            material.SetFloat("_NearLayerShare", nearShare);
            material.SetFloat("_ParallaxNear", 0);
            material.SetFloat("_ParallaxFar", 0);
            material.SetFloat("_ParallaxScale", 1);
            material.SetFloat("_SpacingZoomResponse", 1);
            material.SetFloat("_SizeZoomResponse", 1);
            material.SetFloat("_Brightness", 1);
            material.SetColor("_ColorCool", Color.white);
            material.SetColor("_ColorWarm", Color.white);
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-20, -20, 0), new Vector3(20, -20, 0),
                    new Vector3(20, 20, 0), new Vector3(-20, 20, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            var commands = new CommandBuffer();
            var previousTarget = RenderTexture.active;
            var previousTime = Shader.GetGlobalVector("_Time");
            var previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            try
            {
                target.Create();
                var worstExcess = 0f;
                var brightestBorder = 0f;
                var worstLocation = "";
                foreach (var horizontal in new[] { false, true })
                foreach (var border in new[] { -2f, 0f, 3f })
                for (var phase = 0; phase <= 16; phase++)
                {
                    var time = phase * 0.25f;
                    var rotation = horizontal ? Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)) : Matrix4x4.identity;
                    var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                               Matrix4x4.Translate(new Vector3(-border, 0, 10)) * rotation;
                    commands.Clear();
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(true, true, Color.black);
                    commands.SetViewProjectionMatrices(view, GL.GetGPUProjectionMatrix(
                        Matrix4x4.Ortho(-halfStripWidth, halfStripWidth, -8, 8, 0.1f, 100), true));
                    commands.SetGlobalVector("_WorldSpaceCameraPos",
                        rotation.inverse.MultiplyPoint3x4(new Vector3(border, 0, -10)));
                    commands.SetGlobalVector("_Time", new Vector4(time / 20, time, time * 2, time * 3));
                    commands.DrawMesh(mesh, Matrix4x4.identity, material);
                    Graphics.ExecuteCommandBuffer(commands);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    pixels.Apply();
                    var colors = pixels.GetPixels();
                    for (var y = 0; y < height; y++)
                    {
                        var a = colors[y * width + 2].r;
                        var b = colors[y * width + 3].r;
                        var c = colors[y * width + 4].r;
                        var d = colors[y * width + 5].r;
                        Assert.IsFalse(float.IsNaN(b) || float.IsInfinity(b) ||
                                       float.IsNaN(c) || float.IsInfinity(c));
                        brightestBorder = Mathf.Max(brightestBorder, b, c);
                        var halfPrecision = 0.002f * Mathf.Max(a, b, c, d);
                        var excess = Mathf.Abs(c - b) - 3 * Mathf.Max(Mathf.Abs(b - a), Mathf.Abs(d - c)) - halfPrecision;
                        if (excess <= worstExcess) continue;
                        worstExcess = excess;
                        worstLocation = $"horizontal={horizontal}, border={border}, phase={phase}, row={y}";
                    }
                }
                Assert.Greater(brightestBorder, 0.001f, "The probe must contain halos crossing a cell border.");
                Assert.LessOrEqual(worstExcess, 0.00001f,
                    $"Cell-border jump exceeds adjacent smooth gradients: {worstLocation}");
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                Shader.SetGlobalVector("_Time", previousTime);
                Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
                commands.Release();
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }
    }
}
