using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Combat.Projectiles.Visual
{
    public static class TurboRipperBaker
    {
        private const string Vfx = "Assets/Visuals/Vfx";
        private const string Projectiles = "Assets/Prefabs/Weapons/Projectiles";
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int CoreGlow = Shader.PropertyToID("_CoreGlow");
        private static readonly int Frames = Shader.PropertyToID("_Frames");
        private static readonly int Fps = Shader.PropertyToID("_Fps");
        private static readonly int Crossfade = Shader.PropertyToID("_Crossfade");
        private static readonly int GlowMap = Shader.PropertyToID("_GlowMap");

        [Serializable] private sealed class Export { public Impact turbo; public Impact ripper; public Slug slug; }
        [Serializable] private sealed class Impact
        {
            public string texture;
            public int columns;
            public int rows;
            public float durationSeconds;
            public float solidWidthFraction;
            public float worldLength;
            public bool fadeInTexture;
        }
        [Serializable] private sealed class Slug
        {
            public string texture;
            public int frames;
            public float solidLengthFraction;
            public float worldLength;
        }

        public static void Bake()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-turboRipperExport");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -turboRipperExport with the directory returned by bake.cjs.");
            BakeFromDirectory(args[index + 1]);
        }

        public static void BakeFromDirectory(string source)
        {
            var export = JsonUtility.FromJson<Export>(File.ReadAllText(Path.Combine(source, "bake.json")));
            Wire("TurboLaser", BakeImpact(source, "TurboLaserImpact", export.turbo));
            Wire("RipperSlug", BakeImpact(source, "RipperImpact", export.ripper));
            BakeSlug(source, export.slug);
            AssetDatabase.SaveAssets();
        }

        private static GameObject BakeImpact(string source, string name, Impact settings)
        {
            var material = Material(name, "WeaponExplosions/Materials/PaintedBurst.shader");
            material.SetTexture(BaseMap, Import(source, name, settings.texture));
            material.SetFloat(CoreGlow, 0f);
            EditorUtility.SetDirty(material);
            Directory.CreateDirectory($"{Vfx}/{name}/Prefabs");
            var template = AssetDatabase.LoadAssetAtPath<GameObject>($"{Vfx}/LaserImpact/Prefabs/LaserImpact.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(template);
            try
            {
                root.name = name;
                var particles = root.GetComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.startLifetime = settings.durationSeconds;
                main.startSize = settings.worldLength / settings.solidWidthFraction;
                var color = particles.colorOverLifetime;
                color.enabled = !settings.fadeInTexture;
                var sheet = particles.textureSheetAnimation;
                sheet.numTilesX = settings.columns;
                sheet.numTilesY = settings.rows;
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
                PrefabUtility.RecordPrefabInstancePropertyModifications(particles.GetComponent<ParticleSystemRenderer>());
                return PrefabUtility.SaveAsPrefabAsset(root, $"{Vfx}/{name}/Prefabs/{name}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BakeSlug(string source, Slug settings)
        {
            var texture = Import(source, "RipperSlug", settings.texture);
            var material = Material("RipperSlug", "LaserBolt/Shaders/CrackleFlipbook.shader");
            material.SetTexture(BaseMap, texture);
            material.SetTexture(GlowMap, Texture2D.blackTexture);
            material.SetColor(BaseColor, new Color(1.5f, 1.15f, 0.45f, 1f));
            material.SetFloat(Frames, settings.frames);
            material.SetFloat(Fps, 0f);
            material.SetFloat(Crossfade, 0f);
            EditorUtility.SetDirty(material);
            var path = $"{Projectiles}/RipperSlug.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.transform.Find("Slug");
                if (!visual)
                {
                    visual = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
                    visual.name = "Slug";
                    UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                    visual.SetParent(root.transform, false);
                }
                visual.gameObject.layer = root.layer;
                var height = settings.worldLength / settings.solidLengthFraction;
                visual.localScale = new Vector3(height * texture.width / settings.frames / texture.height / root.transform.localScale.x,
                    height / root.transform.localScale.y, 1f / root.transform.localScale.z);
                var renderer = visual.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var oldRenderer = root.GetComponent<MeshRenderer>();
                if (oldRenderer) UnityEngine.Object.DestroyImmediate(oldRenderer);
                var oldMesh = root.GetComponent<MeshFilter>();
                if (oldMesh) UnityEngine.Object.DestroyImmediate(oldMesh);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Material Material(string name, string shaderPath)
        {
            var path = $"{Vfx}/{name}/Materials/{name}.mat";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(AssetDatabase.LoadAssetAtPath<Shader>($"{Vfx}/{shaderPath}"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D Import(string source, string name, string file)
        {
            var path = $"{Vfx}/{name}/Textures/{file}";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(Path.Combine(source, file), path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Wire(string name, GameObject effect)
        {
            var path = $"{Projectiles}/{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = new SerializedObject(root.GetComponent<ProjectileHitVfx>());
                visual.FindProperty("hitEffect").objectReferenceValue = effect;
                visual.FindProperty("alignWithProjectile").boolValue = true;
                visual.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
