using System;
using System.IO;
using Asteroids.Spawning;
using Asteroids.Visual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Asteroids.Fields.FarField
{
    /// <summary>
    /// Renders every drawn-asteroid surface mesh from each <see cref="FarFieldViews"/> direction into an
    /// albedo and a view-space normal atlas (premultiplied by coverage), then writes the billboard
    /// material and <see cref="FarFieldAtlas"/> and assigns it to the spawn settings. The billboard
    /// relights the albedo with the paint's own lighting, so the bake refuses paints whose surface
    /// colour is not just the base map.
    /// </summary>
    public static class FarFieldAtlasBaker
    {
        private const string SpawnSettingsPath = "Assets/Settings/Asteroids/SpawnSettings.asset";
        private const string OutputParent = "Assets/Visuals/Environment/Asteroids";
        private const string OutputFolder = OutputParent + "/FarField";
        private const string AlbedoPath = OutputFolder + "/FarFieldAlbedo.png";
        private const string NormalsPath = OutputFolder + "/FarFieldNormals.png";
        private const string MaterialPath = OutputFolder + "/FarFieldRock.mat";
        private const string AtlasPath = OutputFolder + "/FarFieldAtlas.asset";
        private const string BakeShaderName = "Hidden/Astronomical/FarFieldBake";
        private const string RockShaderName = "Astronomical/Drawn/FarFieldRock";
        private const int CellPixels = 128;
        private const int Supersample = 4;
        private const float FramingMargin = 1.05f;
        private const int AlbedoPass = 0;
        private const int NormalPass = 1;

        private static readonly string[] LightingFloats =
            { "_ShadowThreshold", "_ShadowSoftness", "_PaletteLighting", "_AmbientStrength" };
        private static readonly string[] LightingColors = { "_BaseColor", "_ShadowColor" };

        private static readonly (string name, float value)[] AlbedoReduction =
        {
            ("_TextureStrength", 1f), ("_LineStrength", 0f), ("_SpecularStrength", 0f),
            ("_DetailAlbedoMapScale", 0f), ("_EmissionStrength", 0f)
        };

        private readonly struct Shape
        {
            public readonly Mesh Surface;
            public readonly Material Paint;

            public Shape(Mesh surface, Material paint)
            {
                Surface = surface;
                Paint = paint;
            }
        }

        [MenuItem("Tools/Asteroids/Bake Far-Field Atlas")]
        public static void Bake()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(SpawnSettingsPath);
            if (!settings) throw new InvalidOperationException($"No spawn settings at {SpawnSettingsPath}.");
            var shapes = ReadShapes(settings);
            ValidatePaints(shapes);

            var columns = FarFieldViews.Count;
            var rows = shapes.Length;
            var width = columns * CellPixels;
            var height = rows * CellPixels;
            var albedo = new Color[width * height];
            var normals = new Color[width * height];
            var halfSizes = new float[rows];

            var bakeShader = Shader.Find(BakeShaderName);
            if (!bakeShader) throw new InvalidOperationException($"Shader '{BakeShaderName}' not found.");
            var bakeMaterial = new Material(bakeShader) { hideFlags = HideFlags.HideAndDontSave };
            var size = CellPixels * Supersample;
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            target.Create();
            var readback = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var block = new MaterialPropertyBlock();
            try
            {
                for (var mesh = 0; mesh < rows; mesh++)
                {
                    var shape = shapes[mesh];
                    var halfSize = MaxVertexDistance(shape.Surface) * FramingMargin;
                    halfSizes[mesh] = halfSize;
                    SetPaintProperties(block, shape.Paint);
                    for (var view = 0; view < columns; view++)
                    {
                        SetViewProperties(block, view, halfSize);
                        var cell = new Vector2Int(view * CellPixels, mesh * CellPixels);
                        Render(shape.Surface, bakeMaterial, AlbedoPass, block, target);
                        DownsampleInto(Read(target, readback), albedo, width, cell);
                        Render(shape.Surface, bakeMaterial, NormalPass, block, target);
                        DownsampleInto(Read(target, readback), normals, width, cell);
                    }
                }
            }
            finally
            {
                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(bakeMaterial);
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder(OutputParent, "FarField");
            WritePng(AlbedoPath, albedo, width, height, gamma: true);
            WritePng(NormalsPath, normals, width, height, gamma: false);
            var albedoTexture = ImportAtlas(AlbedoPath, sRGB: true, Mathf.Max(width, height));
            var normalTexture = ImportAtlas(NormalsPath, sRGB: false, Mathf.Max(width, height));

            var material = WriteMaterial(albedoTexture, normalTexture, columns, rows, shapes[0].Paint);
            var atlas = AssetDatabase.LoadAssetAtPath<FarFieldAtlas>(AtlasPath);
            if (!atlas)
            {
                atlas = ScriptableObject.CreateInstance<FarFieldAtlas>();
                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }
            atlas.Bind(material, halfSizes);
            EditorUtility.SetDirty(atlas);
            settings.farField = atlas;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FarFieldAtlasBaker] Baked {rows} meshes x {columns} views into {OutputFolder}.");
        }

        private static Shape[] ReadShapes(AsteroidSpawnSettings settings)
        {
            if (!settings.asteroidPrefab) throw new InvalidOperationException("Spawn settings have no asteroid prefab.");
            var appearance = settings.asteroidPrefab.GetComponent<DrawnAsteroidAppearance>();
            if (!appearance)
                throw new InvalidOperationException($"'{settings.asteroidPrefab.name}' has no {nameof(DrawnAsteroidAppearance)}.");

            var shapesProperty = new SerializedObject(appearance).FindProperty("shapes");
            var shapes = new Shape[shapesProperty.arraySize];
            for (var i = 0; i < shapes.Length; i++)
            {
                var element = shapesProperty.GetArrayElementAtIndex(i);
                var surface = element.FindPropertyRelative("surface").objectReferenceValue as Mesh;
                var paint = element.FindPropertyRelative("paint").objectReferenceValue as Material;
                if (!surface || !paint) throw new InvalidOperationException($"Drawn asteroid shape {i} lacks a surface mesh or paint.");
                shapes[i] = new Shape(surface, paint);
            }
            if (shapes.Length != settings.meshInfos.Length)
                throw new InvalidOperationException(
                    $"Drawn asteroid has {shapes.Length} shapes but spawn settings list {settings.meshInfos.Length} meshes.");
            return shapes;
        }

        private static void ValidatePaints(Shape[] shapes)
        {
            var reference = shapes[0].Paint;
            foreach (var shape in shapes)
            {
                var paint = shape.Paint;
                foreach (var name in LightingFloats)
                    if (!Mathf.Approximately(paint.GetFloat(name), reference.GetFloat(name)))
                        throw Mismatch(paint, reference, name);
                foreach (var name in LightingColors)
                    if (paint.GetColor(name) != reference.GetColor(name))
                        throw Mismatch(paint, reference, name);
                foreach (var (name, value) in AlbedoReduction)
                    if (!Mathf.Approximately(paint.GetFloat(name), value))
                        throw Unreducible(paint, $"{name} = {paint.GetFloat(name)} (needs {value})");
                if (paint.IsKeywordEnabled("_PAINT_LAYERS")) throw Unreducible(paint, "_PAINT_LAYERS is on");
                if (paint.GetTexture("_WearMap")) throw Unreducible(paint, "it has a wear map");
                var orange = paint.GetVector("_OrangeGain");
                if (orange.x != 1f || orange.y != 1f || orange.z != 1f) throw Unreducible(paint, "_OrangeGain is not 1");
            }
        }

        private static Exception Mismatch(Material paint, Material reference, string property) =>
            new InvalidOperationException(
                $"Paint '{paint.name}' {property} differs from '{reference.name}'; the far-field material carries one lighting set.");

        private static Exception Unreducible(Material paint, string reason) =>
            new InvalidOperationException(
                $"Paint '{paint.name}' cannot bake to a far-field albedo: {reason}.");

        private static float MaxVertexDistance(Mesh mesh)
        {
            var max = 0f;
            foreach (var v in mesh.vertices) max = Mathf.Max(max, v.magnitude);
            return max;
        }

        private static void SetPaintProperties(MaterialPropertyBlock block, Material paint)
        {
            var baseMap = paint.GetTexture("_BaseMap");
            var bumpMap = paint.GetTexture("_BumpMap");
            var scale = paint.GetTextureScale("_BaseMap");
            var offset = paint.GetTextureOffset("_BaseMap");
            block.SetTexture("_BaseMap", baseMap ? baseMap : Texture2D.whiteTexture);
            block.SetVector("_BaseMap_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
            block.SetTexture("_BumpMap", bumpMap ? bumpMap : Texture2D.normalTexture);
            // The paint shader skips the relief entirely without _NORMALMAP; scale 0 is the same flat normal.
            block.SetFloat("_BumpScale", paint.IsKeywordEnabled("_NORMALMAP") ? paint.GetFloat("_BumpScale") : 0f);
        }

        private static void SetViewProperties(MaterialPropertyBlock block, int view, float halfSize)
        {
            var direction = FarFieldViews.Direction(view);
            var up = FarFieldViews.Up(view);
            var eye = direction * (halfSize * 4f);
            var viewMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.LookAt(eye, Vector3.zero, up).inverse;
            var projection = GL.GetGPUProjectionMatrix(
                Matrix4x4.Ortho(-halfSize, halfSize, -halfSize, halfSize, 0.01f, halfSize * 8f), true);
            block.SetMatrix("_BakeMVP", projection * viewMatrix);
            block.SetVector("_BakeRight", FarFieldViews.Right(view));
            block.SetVector("_BakeUp", up);
            block.SetVector("_BakeToCamera", direction);
        }

        private static void Render(Mesh mesh, Material material, int pass, MaterialPropertyBlock block, RenderTexture target)
        {
            var cmd = new CommandBuffer { name = "FarFieldBake" };
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, Color.clear);
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
                cmd.DrawMesh(mesh, Matrix4x4.identity, material, sub, pass, block);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

        private static Color[] Read(RenderTexture target, Texture2D readback)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            RenderTexture.active = previous;
            return readback.GetPixels();
        }

        private static void DownsampleInto(Color[] source, Color[] atlas, int atlasWidth, Vector2Int cell)
        {
            var sourceWidth = CellPixels * Supersample;
            const float weight = 1f / (Supersample * Supersample);
            for (var y = 0; y < CellPixels; y++)
            for (var x = 0; x < CellPixels; x++)
            {
                var sum = Color.clear;
                for (var j = 0; j < Supersample; j++)
                for (var i = 0; i < Supersample; i++)
                    sum += source[(y * Supersample + j) * sourceWidth + x * Supersample + i];
                atlas[(cell.y + y) * atlasWidth + cell.x + x] = sum * weight;
            }
        }

        private static void WritePng(string path, Color[] pixels, int width, int height, bool gamma)
        {
            var encoded = new Color32[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                if (gamma) c = new Color(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g), Mathf.LinearToGammaSpace(c.b), c.a);
                encoded[i] = c;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(encoded);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D ImportAtlas(string path, bool sRGB, int largestSide)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = sRGB;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(largestSide);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material WriteMaterial(Texture2D albedo, Texture2D normals, int columns, int rows, Material paint)
        {
            var shader = Shader.Find(RockShaderName);
            if (!shader) throw new InvalidOperationException($"Shader '{RockShaderName}' not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            material.SetTexture("_AlbedoAtlas", albedo);
            material.SetTexture("_NormalAtlas", normals);
            material.SetFloat("_AtlasColumns", columns);
            material.SetFloat("_AtlasRows", rows);
            foreach (var name in LightingColors) material.SetColor(name, paint.GetColor(name));
            foreach (var name in LightingFloats) material.SetFloat(name, paint.GetFloat(name));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
