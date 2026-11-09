using System.Linq;
using NUnit.Framework;
using Substrate.Services.Locales;
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
        private Vector4 previousTime;
        private CommandBuffer commands;

        [SetUp]
        public void SetUp()
        {
            previousTarget = RenderTexture.active;
            previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            previousTime = Shader.GetGlobalVector("_Time");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Visuals/Locales/Sky/Planets/Planet.shader");
            material = new Material(shader);
            material.SetVector("_PlanetCenter", new Vector4(0, 0, 100000, 0));
            material.SetFloat("_PlanetRadius", 18000);
            material.SetVector("_LightDirection", new Vector4(0, 0, -1, 0));
            material.SetFloat("_AxialTilt", 0);
            planet = new Texture2D(128, 64, TextureFormat.RGBAFloat, false, true)
            {
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (var y = 0; y < planet.height; y++)
            for (var x = 0; x < planet.width; x++)
                planet.SetPixel(x, y, new Color(.4f, .2f, .1f, 0));
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
            Shader.SetGlobalVector("_Time", previousTime);
            commands?.Release();
            if (target) target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(planet);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void SurfaceRespondsToItsLightDirection()
        {
            material.SetVector("_LightDirection", new Vector4(-1, 0, -1, 0));
            var leftLit = Render(Color.black);
            material.SetVector("_LightDirection", new Vector4(1, 0, -1, 0));
            Assert.That(Difference(leftLit, Render(Color.black)), Is.GreaterThan(.02f),
                "Changing the light must shade the spherical surface from the opposite side.");
        }

        [Test]
        public void SurfaceRotatesWhileTheSilhouetteStaysFixed()
        {
            for (var y = 0; y < planet.height; y++)
            for (var x = 0; x < planet.width; x++)
            {
                var color = planet.GetPixel(x, y);
                color.r *= .6f + .4f * Mathf.Sin(x * Mathf.PI * 8 / planet.width);
                planet.SetPixel(x, y, color);
            }
            planet.Apply();
            material.SetFloat("_RotationSpeed", .05f);
            var initial = Render(Color.clear);
            material.SetFloat("_SurfaceLongitude", 45);
            var reframed = Render(Color.clear);
            Assert.That(Difference(initial, reframed), Is.GreaterThan(.02f));
            Assert.That(initial.Select(c => c.a), Is.EqualTo(reframed.Select(c => c.a)));
            material.SetFloat("_SurfaceLongitude", 0);
            var rotated = Render(Color.clear, time: 10);
            Assert.That(Difference(initial, rotated), Is.GreaterThan(.02f),
                "Surface rotation must move cloud markings across the visible hemisphere.");
            Assert.That(initial.Select(c => c.a), Is.EqualTo(rotated.Select(c => c.a)),
                "Surface rotation must not translate or deform the planet silhouette.");
        }

        [Test]
        public void SphereBlocksStarlightIndependentlyOfSurfaceAlphaWhileItsRimTransmits()
        {
            var dark = Render(Color.black);
            var lit = Render(Color.white);
            var center = Height / 2 * Width + Width / 2;
            Assert.That(dark[center].r, Is.EqualTo(.4f).Within(.001f));
            Assert.That(lit[center].r - dark[center].r, Is.LessThan(.0001f),
                "Surface-map alpha must not let stars shine through solid geometry.");
            Assert.That(lit[0].r - dark[0].r, Is.EqualTo(1).Within(.0001f));
            Assert.That(lit.Where((color, i) => color.r - dark[i].r > .1f && color.r - dark[i].r < .9f).Count(),
                Is.GreaterThan(20), "The atmospheric rim must retain partial transmission.");
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
            Assert.That(Difference(baseline, Render(Color.black, cameraX: 1000)), Is.GreaterThan(.01f));
            Assert.That(Render(Color.black, cameraX: 500000).Max(color => color.maxColorComponent), Is.Zero,
                "Leaving the one planet's bounds must not repeat or smear its edge.");
            Assert.That(Difference(baseline, Render(Color.black)), Is.Zero);
        }

        [Test]
        public void LocaleOffsetCarriesItsPlanetButTurningTheCameraDoesNot()
        {
            var baseline = Render(Color.black);
            Assert.That(Difference(baseline, Render(Color.black, cameraX: 20000, localeX: 20000)), Is.Zero,
                "A session offset must carry the distant anchor with its locale.");
            Assert.That(Render(Color.black, cameraYaw: 90).Max(c => c.maxColorComponent), Is.Zero,
                "Looking away from a fixed sphere must remove it from view.");
        }

        [TestCase("VerdantGiant")]
        [TestCase("BlueCrescent")]
        public void OrdinaryTravelKeepsPlanetBetweenDistantLayersAndForegroundAndReturns(string locale)
        {
            var folder = $"Assets/Visuals/Locales/Sky/Locales/{locale}/";
            Object.DestroyImmediate(material);
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(folder + "Planet.mat"));
            var initial = Render(Color.clear, cameraZ: -100);
            var travelled = Render(Color.clear, cameraX: 120, cameraZ: -100);
            var initialEdge = Enumerable.Range(0, Width).First(x => initial[Height / 2 * Width + x].a > .95f);
            var travelledEdge = Enumerable.Range(0, Width).First(x => travelled[Height / 2 * Width + x].a > .95f);
            var planetTravel = initialEdge - travelledEdge;
            Assert.That(planetTravel, Is.InRange(3, 10),
                "Ordinary flight must visibly move the limb without making the planet foreground scenery.");
            Assert.That(Difference(initial, Render(Color.clear, cameraZ: -100)), Is.Zero,
                "Returning the camera must return to the same fixed surface.");

            foreach (var layer in new[] { "DistantStars", "StarField", "FarNebula", "CloseNebula" })
            {
                Object.DestroyImmediate(material);
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(folder + layer + ".mat"));
                GameObject bank = null;
                MaterialPropertyBlock properties = null;
                try
                {
                    if (layer == "FarNebula")
                    {
                        bank = new GameObject("Bank", typeof(MeshRenderer));
                        var renderer = bank.GetComponent<MeshRenderer>();
                        renderer.sharedMaterial = material;
                        bank.AddComponent<CloudBankTile>();
                        properties = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(properties);
                    }
                    var before = Render(Color.black, cameraZ: -100, properties: properties);
                    var after = Render(Color.black, cameraX: 120, cameraZ: -100, properties: properties);
                    Assert.That(before.Max(c => c.maxColorComponent), Is.GreaterThan(.00001f), layer);
                    var travel = FeatureTravel(before, after);
                    if (layer == "CloseNebula")
                        Assert.That(travel, Is.GreaterThan(planetTravel), "Foreground wisps must pass ahead of the planet.");
                    else
                        Assert.That(travel, Is.LessThan(planetTravel), layer + " must remain behind the planet.");
                    Assert.That(Difference(before, Render(Color.black, cameraZ: -100, properties: properties)), Is.Zero);
                }
                finally
                {
                    if (bank) Object.DestroyImmediate(bank);
                }
            }
        }

        private static int FeatureTravel(Color[] before, Color[] after)
        {
            var bestError = float.PositiveInfinity;
            var travel = 0;
            for (var shift = 0; shift <= 24; shift++)
            {
                var error = 0f;
                for (var y = 12; y < Height - 12; y += 2)
                for (var x = 24; x < Width - 24; x += 2)
                {
                    var difference = before[y * Width + x] - after[y * Width + x - shift];
                    error += Mathf.Abs(difference.r) + Mathf.Abs(difference.g) + Mathf.Abs(difference.b);
                }
                if (error >= bestError) continue;
                bestError = error;
                travel = shift;
            }
            return travel;
        }

        private Color[] Render(Color background, float cameraX = 0, float halfHeight = 7, float time = 0,
            float localeX = 0, float cameraYaw = 0, float cameraZ = -10, MaterialPropertyBlock properties = null)
        {
            var cameraPosition = new Vector3(cameraX, 0, cameraZ);
            var halfWidth = halfHeight * Width / Height;
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true, true, background);
            commands.SetViewProjectionMatrices(
                Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(cameraPosition,
                    Quaternion.Euler(0, cameraYaw, 0), Vector3.one).inverse,
                GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight, .1f, 100), true));
            commands.SetGlobalVector("_WorldSpaceCameraPos", cameraPosition);
            commands.SetGlobalVector("_Time", new Vector4(time / 20, time, time * 2, time * 3));
            commands.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(localeX, 0, 0)), material, 0, 0, properties);
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
