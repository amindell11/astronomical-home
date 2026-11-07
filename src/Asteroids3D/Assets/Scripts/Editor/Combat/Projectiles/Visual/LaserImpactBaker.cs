using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Utils;

namespace Combat.Projectiles.Visual
{
    public static class LaserImpactBaker
    {
        private const string Root = "Assets/Visuals/Vfx/LaserImpact";

        [Serializable]
        private sealed class Settings
        {
            public string texture;
            public int columns;
            public int rows;
            public float durationSeconds;
            public float solidWidthFraction;
        }

        [MenuItem("Tools/Art/Bake laser impact")]
        private static void BakeFromFolder()
        {
            var source = EditorUtility.OpenFolderPanel("Select laser impact export", "", "");
            if (source.Length > 0) BakeFromDirectory(source);
        }

        public static void Bake()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-laserImpactExport");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Pass -laserImpactExport with the directory returned by bake.cjs.");
            BakeFromDirectory(args[index + 1]);
        }

        public static void BakeFromDirectory(string source)
        {
            var settings = JsonUtility.FromJson<Settings>(File.ReadAllText(Path.Combine(source, "bake.json")));
            Directory.CreateDirectory($"{Root}/Textures");
            Directory.CreateDirectory($"{Root}/Materials");
            Directory.CreateDirectory($"{Root}/Prefabs");
            var texturePath = $"{Root}/Textures/LaserImpact.png";
            File.Copy(Path.Combine(source, settings.texture), texturePath, true);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var materialPath = $"{Root}/Materials/LaserImpact.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                    "Assets/Visuals/Vfx/WeaponExplosions/Materials/PaintedBurst.shader");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_CoreGlow", 0f);
            EditorUtility.SetDirty(material);

            var root = new GameObject("LaserImpact");
            GameObject prefab;
            try
            {
                var particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.duration = 0.01f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = settings.durationSeconds;
                main.startSpeed = 0f;
                main.startSize = 1.2f / settings.solidWidthFraction;
                main.maxParticles = 1;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = particles.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var shape = particles.shape;
                shape.enabled = false;
                var sheet = particles.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = settings.columns;
                sheet.numTilesY = settings.rows;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.alignment = ParticleSystemRenderSpace.Local;
                renderer.maxParticleSize = 1f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend
                });
                root.AddComponent<PooledVFX>();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Prefabs/LaserImpact.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            Wire("Laser", prefab, true);
            Wire("TurboLaser", null, false);
            AssetDatabase.SaveAssets();
        }

        private static void Wire(string name, GameObject effect, bool align)
        {
            var path = $"Assets/Prefabs/Weapons/Projectiles/{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = new SerializedObject(root.GetComponent<ProjectileHitVfx>());
                visual.FindProperty("hitEffect").objectReferenceValue = effect;
                visual.FindProperty("alignWithProjectile").boolValue = align;
                visual.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
