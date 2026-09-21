using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering
{
    [TestFixture, Category("Sectors"), Category("RequiresGraphics")]
    public class BackgroundAtmosphereRenderTests
    {
        private Material material;
        private Mesh mesh;
        private RenderTexture target;
        private Texture2D pixels;
        private CommandBuffer commands;
        private RenderTexture previousTarget;
        private Vector4 previousTime;
        private Vector4 previousCamera;

        [SetUp]
        public void SetUp()
        {
            previousTarget = RenderTexture.active;
            previousTime = Shader.GetGlobalVector("_Time");
            previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            material = new Material(Shader.Find("Custom/StarField"));
            material.SetFloat("_StarDensity", 0);
            mesh = new Mesh
            {
                vertices = new[] { new Vector3(-100, -100), new Vector3(100, -100),
                    new Vector3(100, 100), new Vector3(-100, 100) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            target = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            target.Create();
            pixels = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
            commands = new CommandBuffer();
        }

        [TearDown]
        public void TearDown()
        {
            RenderTexture.active = previousTarget;
            Shader.SetGlobalVector("_Time", previousTime);
            Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
            commands?.Release();
            if (target) target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void Nebula_HasDarkGapsEvolvesAndReturnsAfterCameraTravel()
        {
            Assert.That(Render(0).Max(c => c.maxColorComponent), Is.Zero);
            material.SetFloat("_NebulaStrength", 0.12f);
            var baseline = Render(0);
            Assert.Greater(baseline.Max(c => c.r), 0.002f, "Clouds must be visible.");
            Assert.Less(baseline.Min(c => c.r), 0.001f, "Clouds must leave dark gaps.");
            Assert.Greater(Difference(baseline, Render(60)), 0.0001f, "Clouds must evolve.");
            Assert.Greater(Difference(baseline, Render(0, 70)), 0.0001f, "Camera travel must reveal new clouds.");
            Assert.That(Difference(baseline, Render(0)), Is.Zero, "Returning must not accumulate drift.");
            material.SetFloat("_NebulaStrength", 0);
            Assert.That(Render(60).Max(c => c.maxColorComponent), Is.Zero);
        }

        [Test]
        public void ShootingStars_AppearMoveAndLeaveQuietIntervals()
        {
            material.SetFloat("_ShootingBrightness", 0.65f);
            var litFrames = 0;
            var quietFrames = 0;
            var movingFrames = 0;
            Color[] previous = null;
            for (var step = 0; step < 100; step++)
            {
                var frame = Render(step * 0.25f);
                if (frame.Max(c => c.r) > 0.003f) litFrames++;
                else quietFrames++;
                if (previous != null && Difference(previous, frame) > 0.003f) movingFrames++;
                previous = frame;
            }
            Assert.Greater(litFrames, 0, "A streak must appear during the sampled interval.");
            Assert.Greater(quietFrames, litFrames, "Quiet frames must dominate.");
            Assert.Greater(movingFrames, 1, "A streak must change across frames.");
            material.SetFloat("_ShootingBrightness", 0);
            Assert.That(Render(10).Max(c => c.maxColorComponent), Is.Zero);
        }

        private Color[] Render(float time, float cameraX = 0)
        {
            var cameraPosition = new Vector3(cameraX, 0, -10);
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true, true, Color.black);
            commands.SetViewProjectionMatrices(
                Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.Translate(-cameraPosition),
                GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-32, 32, -32, 32, 0.1f, 100), true));
            commands.SetGlobalVector("_WorldSpaceCameraPos", cameraPosition);
            commands.SetGlobalVector("_Time", new Vector4(time / 20, time, time * 2, time * 3));
            commands.DrawMesh(mesh, Matrix4x4.identity, material);
            Graphics.ExecuteCommandBuffer(commands);
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            pixels.Apply();
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            return pixels.GetPixels();
        }

        private static float Difference(Color[] first, Color[] second)
        {
            var maximum = 0f;
            for (var i = 0; i < first.Length; i++)
                maximum = Mathf.Max(maximum, Mathf.Abs(first[i].r - second[i].r));
            return maximum;
        }
    }
}
