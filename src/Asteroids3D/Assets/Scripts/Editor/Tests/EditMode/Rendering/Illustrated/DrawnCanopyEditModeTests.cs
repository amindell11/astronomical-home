using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Ships"), Category("RequiresGraphics")]
    public sealed class DrawnCanopyEditModeTests
    {
        private GameObject root;
        private Renderer subject;
        private Camera camera;
        private Light key;
        private Material material;
        private Mesh surfaceMesh;
        private RenderTexture target;
        private Texture2D image;
        private RenderPipelineAsset priorPipeline;
        private RenderTexture priorTarget;
        private Light priorSun;
        private bool priorAsync;

        [SetUp]
        public void SetUp()
        {
            priorPipeline = QualitySettings.renderPipeline;
            priorTarget = RenderTexture.active;
            priorSun = RenderSettings.sun;
            priorAsync = ShaderUtil.allowAsyncCompilation;
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                "Assets/Settings/Rendering/URP-HighFidelity.asset");
            ShaderUtil.allowAsyncCompilation = false;
            root = new GameObject("Canopy test");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(root.transform, false);
            quad.layer = 31;
            subject = quad.GetComponent<Renderer>();
            target = new RenderTexture(128, 128, 24);
            image = new Texture2D(128, 128, TextureFormat.RGB24, false);
            camera = new GameObject("Canopy camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.position = new Vector3(0, 0, -2);
            camera.orthographic = true;
            camera.orthographicSize = .6f;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetTexture = target;
            key = new GameObject("Canopy key", typeof(Light)).GetComponent<Light>();
            key.transform.SetParent(root.transform, false);
            key.type = LightType.Directional;
            key.cullingMask = 1 << 31;
            key.intensity = 1;
            RenderSettings.sun = key;
        }

        [TearDown]
        public void TearDown()
        {
            QualitySettings.renderPipeline = priorPipeline;
            RenderTexture.active = priorTarget;
            RenderSettings.sun = priorSun;
            ShaderUtil.allowAsyncCompilation = priorAsync;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(surfaceMesh);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        [TestCase("Crimson", "Hull", 0, false)]
        [TestCase("Nightshade", "Canopy", 0, false)]
        [TestCase("Valis", "Valis", 5, false)]
        [TestCase("Vanguard", "Canopy", 0, false)]
        [TestCase("Valis", "Fuselage and canopy", 5, true)]
        public void Prefab_GlassProjectionMatchesItsMaterialSubmesh(string ship, string rendererName, int index, bool debris)
        {
            string path = debris ? "Assets/Visuals/Ships/" + ship + "/Breakup/" + ship + "Breakup.prefab"
                : "Assets/Prefabs/Ships/" + ship + ".prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            Mesh baked = null;
            try
            {
                var renderer = prefab.GetComponentsInChildren<Renderer>(true).Single(r => r.name == rendererName);
                var glass = renderer.sharedMaterials[index];
                Assert.That(glass.shader.name, Is.EqualTo("Astronomical/Drawn/Canopy"));
                Mesh mesh;
                var skin = renderer as SkinnedMeshRenderer;
                if (skin)
                {
                    baked = new Mesh();
                    skin.BakeMesh(baked);
                    mesh = baked;
                }
                else mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                var triangles = mesh.GetTriangles(index);
                var bounds = new Bounds(vertices[triangles[0]], Vector3.zero);
                foreach (int vertex in triangles) bounds.Encapsulate(vertices[vertex]);
                var projection = glass.GetVector("_ReflectionBounds");
                Assert.That((bounds.center.x - projection.x) * projection.z, Is.EqualTo(0).Within(.001f));
                Assert.That((bounds.center.y - projection.y) * projection.w, Is.EqualTo(0).Within(.001f));
                Assert.That(bounds.size.x * projection.z, Is.EqualTo(1).Within(.001f));
                Assert.That(bounds.size.y * projection.w, Is.EqualTo(1).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(baked);
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        [TestCase("jade-iris")]
        [TestCase("jade-gray")]
        [TestCase("ivory-lavender")]
        public void Valis_RetainsAuthoredMaskStrengths(string palette)
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Valis/Canopy/" + palette + ".mat");
            Assert.That(glass.GetVector("_ReflectionLayerStrengths"), Is.EqualTo(new Vector4(1.02f, .2f, 1.5f, 0)));
        }

        [TestCase("Nightshade/Canopy/Canopy.mat")]
        [TestCase("Valis/Canopy/jade-iris.mat")]
        [TestCase("Valis/Canopy/jade-gray.mat")]
        [TestCase("Valis/Canopy/ivory-lavender.mat")]
        public void PaintedGlass_RemainsFixedWithoutReflections(string path)
        {
            UseMaterial(path);
            material.SetFloat("_DrawnReflectionStrength", 0);
            key.transform.rotation = Quaternion.Euler(0, -20, 0);
            var glass = Read();
            key.transform.rotation = Quaternion.Euler(0, 20, 0);
            Assert.That(Read(), Is.EqualTo(glass));
            var centerRow = glass.Skip(64 * 128 + 32).Take(64).Select(c => c.b).ToArray();
            Assert.That(centerRow.Max(), Is.GreaterThan(material.GetColor("_ReflectionColor").b * .7f),
                "The authored glass color must remain without its glints.");
            Assert.That(centerRow.Max() - centerRow.Min(), Is.GreaterThan(material.GetColor("_ReflectionColor").b * .25f),
                "The fixed glass must retain its painted shading.");
        }

        [TestCase(-60)]
        [TestCase(60)]
        public void Crimson_StripesContinuePastGlassAtExtremePitch(float pitch)
        {
            UseMaterial("Crimson/Canopy/Canopy.mat");
            key.transform.rotation = Quaternion.Euler(pitch, 0, 0);
            var pixels = ReadReflection();
            foreach (int row in new[] { 12, 115 })
                Assert.That(pixels.Skip(row * 128 + 32).Take(64).Max(c => c.grayscale), Is.GreaterThan(.03f),
                    "The painted stripes must continue through both ends of the glass.");
        }

        [TestCase("Crimson/Canopy/Canopy.mat", false)]
        [TestCase("Crimson/Canopy/Canopy.mat", true)]
        [TestCase("Nightshade/Canopy/Canopy.mat", false)]
        [TestCase("Nightshade/Canopy/Canopy.mat", true)]
        [TestCase("Valis/Canopy/jade-iris.mat", false)]
        [TestCase("Vanguard/Materials/Canopy.mat", false)]
        public void Reflection_MovesAcrossGlassWhenLightTurns(string path, bool underside)
        {
            UseMaterial(path, underside);
            key.transform.rotation = Quaternion.Euler(0, -20, 0);
            float left = LuminanceCentroid(ReadReflection());
            key.transform.rotation = Quaternion.Euler(0, 20, 0);
            float right = LuminanceCentroid(ReadReflection());
            Assert.That(left - right, Is.GreaterThan(.06f), "The drawing must move, not only brighten.");
        }

        [Test]
        public void Valis_UnpaintedSideKeepsGlassWithoutInventedReflections()
        {
            UseMaterial("Valis/Canopy/jade-iris.mat", true);
            var glass = Read();
            material.SetFloat("_DrawnReflectionStrength", 0);
            Assert.That(Read(), Is.EqualTo(glass));
            Assert.That(glass.Max(c => c.b), Is.GreaterThan(.1f));
        }

        [TestCase(-55)]
        [TestCase(55)]
        public void Nightshade_SecondaryGlintsEnterDuringRoll(float roll)
        {
            UseMaterial("Nightshade/Canopy/Canopy.mat");
            camera.transform.position = Quaternion.Euler(0, roll, 0) * new Vector3(0, 0, -2);
            camera.transform.LookAt(Vector3.zero);
            key.transform.rotation = camera.transform.rotation;
            var withAccents = Read();
            material.SetVector("_SecondaryGlintLeft", new Vector4(.5f, .5f, 1, 0));
            material.SetVector("_SecondaryGlintRight", new Vector4(.5f, .5f, 1, 0));
            var withoutAccents = Read();
            Assert.That(withAccents.Where((color, i) => color.grayscale - withoutAccents[i].grayscale > .01f).Count(),
                Is.GreaterThan(16), "A roll must bring smaller painted glints into view.");
        }

        [TestCase("Crimson/Canopy/Canopy.mat")]
        [TestCase("Nightshade/Canopy/Canopy.mat")]
        [TestCase("Valis/Canopy/jade-iris.mat")]
        [TestCase("Vanguard/Materials/Canopy.mat")]
        public void Reflection_SurfaceDirectionChangesDrawingUnderSymmetricLighting(string path)
        {
            UseMaterial(path);
            var filter = subject.GetComponent<MeshFilter>();
            surfaceMesh = Object.Instantiate(filter.sharedMesh);
            filter.sharedMesh = surfaceMesh;
            camera.transform.position = Quaternion.Euler(50, 0, 0) * new Vector3(0, 0, -2);
            camera.transform.LookAt(Vector3.zero);
            key.transform.rotation = camera.transform.rotation;
            surfaceMesh.normals = Enumerable.Repeat(new Vector3(.6f, 0, -.8f), surfaceMesh.vertexCount).ToArray();
            float right = LuminanceCentroid(ReadReflection());
            surfaceMesh.normals = Enumerable.Repeat(new Vector3(-.6f, 0, -.8f), surfaceMesh.vertexCount).ToArray();
            float left = LuminanceCentroid(ReadReflection());
            Assert.That(right - left, Is.GreaterThan(.04f),
                "Equal lighting on opposite slopes must still displace the reflected drawing.");
        }

        [TestCase("Crimson/Canopy/Canopy.mat")]
        [TestCase("Nightshade/Canopy/Canopy.mat")]
        [TestCase("Valis/Canopy/jade-iris.mat")]
        [TestCase("Vanguard/Materials/Canopy.mat")]
        public void RendererOverrides_FlashRestoresDrawingAndDebrisDisappears(string path)
        {
            UseMaterial(path);
            var baseline = Read();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Color.red);
            block.SetFloat("_DamageFlash", 1);
            subject.SetPropertyBlock(block);
            var flash = Read()[64 * 128 + 64];
            Assert.That(flash.r, Is.GreaterThan(.8f));
            Assert.That(flash.g + flash.b, Is.LessThan(.02f));
            block.SetColor("_BaseColor", Color.white);
            block.SetFloat("_DamageFlash", 0);
            subject.SetPropertyBlock(block);
            Assert.That(Read(), Is.EqualTo(baseline));
            block.SetFloat("_DebrisVisibility", 0);
            subject.SetPropertyBlock(block);
            Assert.That(Read().Max(c => c.maxColorComponent), Is.EqualTo(0));
        }

        private void UseMaterial(string path, bool underside = false)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/" + path));
            material.SetVector("_ReflectionBounds", new Vector4(0, 0, 1, 1));
            subject.sharedMaterial = material;
            if (underside)
            {
                var filter = subject.GetComponent<MeshFilter>();
                surfaceMesh = Object.Instantiate(filter.sharedMesh);
                surfaceMesh.normals = surfaceMesh.normals.Select(n => -n).ToArray();
                surfaceMesh.triangles = surfaceMesh.triangles.Reverse().ToArray();
                filter.sharedMesh = surfaceMesh;
                subject.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }
        }

        private Color[] Read()
        {
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            image.Apply();
            return image.GetPixels();
        }

        private Color[] ReadReflection()
        {
            float strength = material.GetFloat("_DrawnReflectionStrength");
            material.SetFloat("_DrawnReflectionStrength", 0);
            var glass = Read();
            material.SetFloat("_DrawnReflectionStrength", strength);
            var reflected = Read();
            return reflected.Select((color, i) => new Color(Mathf.Max(0, color.r - glass[i].r),
                Mathf.Max(0, color.g - glass[i].g), Mathf.Max(0, color.b - glass[i].b))).ToArray();
        }

        private static float LuminanceCentroid(Color[] pixels)
        {
            float energy = 0, weightedX = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                float luminance = pixels[i].grayscale;
                energy += luminance;
                weightedX += luminance * (i % 128) / 128;
            }
            Assert.That(energy, Is.GreaterThan(10), "The reflection render must contain visible artwork.");
            return weightedX / energy;
        }
    }
}
