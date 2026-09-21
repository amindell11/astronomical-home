using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Sectors")]
    public class StarfieldShaderEditModeTests
    {
        private const string MaterialPath = "Assets/Visuals/Environment/Sky/StarFieldMaterial.mat";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";

        private static Material LoadMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, $"Starfield material missing at {MaterialPath}.");
            return material;
        }

        [Test]
        public void Material_MatchesProceduralShaderContract()
        {
            var material = LoadMaterial();

            Assert.AreEqual("Custom/StarField", material.shader.name);
            Assert.AreEqual(2950, material.renderQueue,
                "The starfield must render after the skybox and before ordinary transparent effects.");

            var properties = new[]
            {
                "_Seed", "_StarDensity", "_CellScale", "_StarSizeMin", "_StarSizeMax",
                "_PositionJitter", "_ParallaxFar", "_ParallaxNear", "_NearLayerShare",
                "_ColorCool", "_ColorWarm", "_WarmColorShare", "_Brightness", "_HaloSize",
                "_HaloStrength", "_TwinkleAmount", "_TwinkleDurationMin", "_TwinkleDurationMax"
            };

            foreach (var property in properties)
                Assert.IsTrue(material.HasProperty(property), $"Starfield shader is missing {property}.");

            Assert.IsFalse(material.HasProperty("_CullDistance"));
            Assert.IsFalse(material.HasProperty("_GridDensity"));
            Assert.IsFalse(material.HasProperty("_ParallaxStrength"));
            Assert.IsFalse(material.HasProperty("_StartingOffset"));
        }

        [Test]
        public void Material_DefaultsDescribeAValidDepthRange()
        {
            var material = LoadMaterial();

            Assert.Greater(material.GetFloat("_TwinkleDurationMin"), 0);
            Assert.GreaterOrEqual(material.GetFloat("_TwinkleDurationMax"), material.GetFloat("_TwinkleDurationMin"));
            Assert.Greater(material.GetFloat("_CellScale"), 0);
            Assert.Greater(material.GetFloat("_StarSizeMin"), 0);
            Assert.GreaterOrEqual(material.GetFloat("_StarSizeMax"), material.GetFloat("_StarSizeMin"));
            Assert.GreaterOrEqual(material.GetFloat("_ParallaxNear"), material.GetFloat("_ParallaxFar"));
            Assert.That(material.GetFloat("_StarDensity"), Is.InRange(0f, 1f));
            Assert.That(material.GetFloat("_NearLayerShare"), Is.InRange(0f, 1f));
        }

        [Test]
        [Category("RequiresGraphics")]
        public void Material_RenderKeepsCellBoundariesDark()
        {
            const int width = 1920;
            const int height = 1080;
            const float halfHeight = 7f;
            const float cellScale = 0.8f;
            var material = new Material(LoadMaterial());
            material.SetFloat("_NebulaStrength", 0);
            material.SetFloat("_ShootingBrightness", 0);
            material.SetFloat("_Seed", 0f);
            material.SetFloat("_StarDensity", 0.12f);
            material.SetFloat("_CellScale", cellScale);
            material.SetFloat("_PositionJitter", 0.5f);
            material.SetFloat("_StarSizeMin", 0.012f);
            material.SetFloat("_StarSizeMax", 0.045f);
            material.SetFloat("_HaloSize", 2f);
            material.SetFloat("_HaloStrength", 0.2f);
            material.SetFloat("_NearLayerShare", 0.35f);
            material.SetFloat("_TwinkleAmount", 0.2f);
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-20, -20, 0), new Vector3(20, -20, 0),
                    new Vector3(20, 20, 0), new Vector3(-20, 20, 0)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var commands = new UnityEngine.Rendering.CommandBuffer();
            var previousTarget = RenderTexture.active;
            var previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            var previousTime = Shader.GetGlobalVector("_Time");
            try
            {
                target.Create();
                var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                           Matrix4x4.Translate(new Vector3(0, 0, 10));
                var halfWidth = halfHeight * width / height;
                var projection = GL.GetGPUProjectionMatrix(
                    Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight, 0.1f, 100), true);
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.black);
                commands.SetViewProjectionMatrices(view, projection);
                commands.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(0, 0, -10, 1));
                commands.SetGlobalVector("_Time", Vector4.zero);
                commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                var colors = pixels.GetPixels32();
                var litPixels = 0;
                var litBoundaryPixels = 0;
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var color = colors[y * width + x];
                    if (Mathf.Max(color.r, Mathf.Max(color.g, color.b)) <= 8) continue;
                    litPixels++;
                    var cellX = ((x + 0.5f) / width * 2 - 1) * halfWidth * cellScale;
                    var cellY = ((y + 0.5f) / height * 2 - 1) * halfHeight * cellScale;
                    if (Mathf.Abs(cellX - Mathf.Round(cellX)) < 0.04f ||
                        Mathf.Abs(cellY - Mathf.Round(cellY)) < 0.04f)
                        litBoundaryPixels++;
                }
                Assert.Greater(litPixels, 100, "The shader must render stars, not a blank frame.");
                Assert.Zero(litBoundaryPixels, "These stars fit inside their cells; lit cell edges are streaks.");
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
                Shader.SetGlobalVector("_Time", previousTime);
                commands.Release();
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        [Category("RequiresGraphics")]
        public void Material_TwinkleDurationIsAFullCycleInSeconds()
        {
            var material = new Material(LoadMaterial());
            material.SetFloat("_NebulaStrength", 0);
            material.SetFloat("_ShootingBrightness", 0);
            material.SetFloat("_TwinkleDurationMin", 4);
            material.SetFloat("_TwinkleDurationMax", 4);
            material.SetFloat("_TwinkleAmount", 1);
            material.SetFloat("_StarDensity", 1);
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-2, -2, 0), new Vector3(2, -2, 0),
                    new Vector3(2, 2, 0), new Vector3(-2, 2, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            var target = new RenderTexture(256, 256, 0);
            var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            var previousTime = Shader.GetGlobalVector("_Time");
            var previousCamera = Shader.GetGlobalVector("_WorldSpaceCameraPos");
            Color32[] Render(float time)
            {
                var commands = new UnityEngine.Rendering.CommandBuffer();
                try
                {
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(true, true, Color.black);
                    commands.SetViewProjectionMatrices(
                        Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.Translate(new Vector3(0, 0, 10)),
                        GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-2, 2, -2, 2, 0.1f, 100), true));
                    commands.SetGlobalVector("_WorldSpaceCameraPos", new Vector4(0, 0, -10, 1));
                    commands.SetGlobalVector("_Time", new Vector4(time / 20, time, time * 2, time * 3));
                    commands.DrawMesh(mesh, Matrix4x4.identity, material);
                    Graphics.ExecuteCommandBuffer(commands);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                    pixels.Apply();
                    return pixels.GetPixels32();
                }
                finally { commands.Release(); }
            }
            try
            {
                target.Create();
                var initial = Render(0);
                var halfCycle = Render(2);
                var fullCycle = Render(4);
                var changed = 0;
                for (var i = 0; i < initial.Length; i++)
                {
                    if (!initial[i].Equals(halfCycle[i])) changed++;
                    Assert.That(Mathf.Abs(initial[i].r - fullCycle[i].r), Is.LessThanOrEqualTo(1));
                    Assert.That(Mathf.Abs(initial[i].g - fullCycle[i].g), Is.LessThanOrEqualTo(1));
                    Assert.That(Mathf.Abs(initial[i].b - fullCycle[i].b), Is.LessThanOrEqualTo(1));
                }
                Assert.Greater(changed, 10, "The cycle check must contain animated stars.");
                material.SetFloat("_TwinkleAmount", 0);
                CollectionAssert.AreEqual(Render(0), Render(1), "Zero amount must disable all twinkle motion.");
            }
            finally
            {
                RenderTexture.active = previousTarget;
                Shader.SetGlobalVector("_Time", previousTime);
                Shader.SetGlobalVector("_WorldSpaceCameraPos", previousCamera);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void AuthoredStarfield_RidesTheObserverCamera_WithTheProductionMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObserverCamPrefabPath);
            Assert.IsNotNull(prefab, $"Observer camera prefab missing at {ObserverCamPrefabPath}.");

            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            Assert.IsNotNull(renderer, $"No starfield SpriteRenderer found under {ObserverCamPrefabPath}.");
            Assert.AreSame(LoadMaterial(), renderer.sharedMaterial);
        }
    }
}
