using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering.Locales
{
    [Category("Sectors"), Category("RequiresGraphics")]
    public sealed class PlanetRenderTests
    {
        private const int Width = 384;
        private const int Height = 216;
        private Material material;
        private Mesh mesh;
        private Texture2D planet;
        private Texture2D readback;
        private RenderTexture target;
        private RenderTexture previousTarget;
        private Vector4 previousCamera;
        private CommandBuffer commands;

        [SetUp]
        public void SetUp()
        {
            previousTarget = RenderTexture.active;
            previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Visuals/Locales/Sky/Planets/Planet.shader");
            material = new Material(shader);
            material.SetVector("_PlanetCenter", Vector4.zero);
            material.SetFloat("_PlanetSize", 6);
            material.SetFloat("_Parallax", .01f);
            planet = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var radius = new Vector2((x + .5f) / 64 - .5f, (y + .5f) / 64 - .5f).magnitude;
                var alpha = Mathf.Clamp01((.48f - radius) / .12f) * (250f / 255);
                planet.SetPixel(x, y, new Color(.4f, .2f, .1f, alpha));
            }
            planet.Apply();
            material.SetTexture("_MainTex", planet);
            mesh = new Mesh
            {
                vertices = new[] { new Vector3(-1, -1), new Vector3(-1, 3), new Vector3(3, -1) },
                triangles = new[] { 0, 1, 2 }
            };
            target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create();
            readback = new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true);
            commands = new CommandBuffer();
        }

        [TearDown]
        public void TearDown()
        {
            RenderTexture.active = previousTarget;
            Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
            commands?.Release();
            if (target) target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(planet);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void DiscBlocksStarlightWhileItsRimAndSurroundingsRemainTransparent()
        {
            var dark = Render(Color.black);
            var lit = Render(Color.white);
            var center = Height / 2 * Width + Width / 2;
            Assert.That(dark[center].r, Is.EqualTo(.4f).Within(.001f));
            Assert.That(lit[center].r - dark[center].r, Is.LessThan(.0001f),
                "Painted interiors with alpha 250/255 must fully occlude the star layers.");
            Assert.That(lit[0].r - dark[0].r, Is.EqualTo(1).Within(.0001f));
            Assert.That(lit.Where((color, i) => color.r - dark[i].r > .1f && color.r - dark[i].r < .9f).Count(),
                Is.GreaterThan(100), "The atmospheric rim must retain partial transmission.");
        }

        [Test]
        public void PlanetIsRoundAndKeepsItsApparentSizeThroughZoom()
        {
            var baseline = Render(Color.black);
            Assert.That(Difference(baseline, Render(Color.black, halfHeight: 3.5f)), Is.LessThan(.0001f));
            Assert.That(Difference(baseline, Render(Color.black, halfHeight: 28)), Is.LessThan(.0001f));
            var horizontal = Enumerable.Range(0, Width).Count(x => baseline[Height / 2 * Width + x].r > .01f);
            var vertical = Enumerable.Range(0, Height).Count(y => baseline[y * Width + Width / 2].r > .01f);
            Assert.That(horizontal, Is.GreaterThan(50), "A blank render cannot establish scale.");
            Assert.That(horizontal, Is.EqualTo(vertical).Within(2), "A widescreen camera must not stretch the disc.");
        }

        [Test]
        public void CameraTravelMovesOnePlanetWithoutRepeatingAndReturnsToItsOriginalPosition()
        {
            var baseline = Render(Color.black);
            Assert.That(Difference(baseline, Render(Color.black, cameraX: 20)), Is.GreaterThan(.01f));
            Assert.That(Render(Color.black, cameraX: 5000).Max(color => color.maxColorComponent), Is.Zero,
                "Leaving the one planet's bounds must not repeat or smear its edge.");
            Assert.That(Difference(baseline, Render(Color.black)), Is.Zero);
        }

        private Color[] Render(Color background, float cameraX = 0, float halfHeight = 7)
        {
            var cameraPosition = new Vector3(cameraX, 0, -10);
            var halfWidth = halfHeight * Width / Height;
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true, true, background);
            commands.SetViewProjectionMatrices(
                Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.Translate(-cameraPosition),
                GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight, .1f, 100), true));
            commands.SetGlobalVector("_WorldSpaceCameraPos", cameraPosition);
            commands.DrawMesh(mesh, Matrix4x4.identity, material);
            Graphics.ExecuteCommandBuffer(commands);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readback.Apply();
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            return readback.GetPixels();
        }

        private static float Difference(Color[] first, Color[] second) =>
            first.Zip(second, (a, b) => Mathf.Abs(a.r - b.r)).Max();
    }
}
