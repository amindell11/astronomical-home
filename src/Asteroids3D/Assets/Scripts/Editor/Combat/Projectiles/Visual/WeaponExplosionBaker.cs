using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Utils;

namespace Combat.Projectiles.Visual
{
    public static class WeaponExplosionBaker
    {
        private const string Root = "Assets/Visuals/Vfx/WeaponExplosions";

        [Serializable]
        private sealed class Settings
        {
            public float missileTailSeconds;
            public float mineIgnitionSeconds;
            public float mineFill;
            public float mineGlow;
        }

        [MenuItem("Tools/Art/Bake weapon explosions")]
        public static void Bake()
        {
            var source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../art/vfx/weapon-explosions/out"));
            var settings = JsonUtility.FromJson<Settings>(File.ReadAllText(Path.Combine(source, "settings.json")));
            Directory.CreateDirectory($"{Root}/Textures");
            Directory.CreateDirectory($"{Root}/Prefabs");
            foreach (var name in new[] { "Missile", "MineIgnition", "MineDisc" })
            {
                var target = $"{Root}/Textures/{name}.png";
                File.Copy(Path.Combine(source, $"{name}.png"), target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
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
            }

            var burstShader = AssetDatabase.LoadAssetAtPath<Shader>($"{Root}/Materials/PaintedBurst.shader");
            var missileMaterial = Material("Missile", burstShader, "Missile");
            var ignitionMaterial = Material("MineIgnition", burstShader, "MineIgnition");
            var discMaterial = Material("MineDisc", AssetDatabase.LoadAssetAtPath<Shader>($"{Root}/Materials/SeismicDisc.shader"), "MineDisc");
            discMaterial.SetFloat("_FillOpacity", settings.mineFill);
            discMaterial.SetFloat("_EdgeGain", settings.mineGlow);
            EditorUtility.SetDirty(discMaterial);
            var missileBurst = Burst("MissileExplosion", missileMaterial, settings.missileTailSeconds, 6f, 8);
            var mineBurst = Burst("MineIgnition", ignitionMaterial, settings.mineIgnitionSeconds, 5f, 4);

            EditPrefab("Assets/Prefabs/Weapons/Projectiles/MissileProjectile.prefab", root =>
            {
                var visual = new SerializedObject(root.GetComponent<MissileVisual>());
                visual.FindProperty("explosionPrefab").objectReferenceValue = missileBurst;
                visual.ApplyModifiedPropertiesWithoutUndo();
            });
            EditPrefab("Assets/Prefabs/Weapons/Projectiles/ConcussionWave.prefab", root =>
            {
                var visual = new SerializedObject(root.GetComponent<ConcussionWaveVisual>());
                visual.FindProperty("burstPrefab").objectReferenceValue = mineBurst;
                visual.ApplyModifiedPropertiesWithoutUndo();
                var disc = root.transform.Find("SeismicDisc");
                if (!disc)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.name = "SeismicDisc";
                    disc = quad.transform;
                    disc.SetParent(root.transform, false);
                }
                disc.localScale = Vector3.one * (2f / 0.9f);
                var renderer = disc.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = discMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                root.GetComponent<LineRenderer>().widthMultiplier = 0.06f;
            });
            AssetDatabase.SaveAssets();
            Debug.Log("Weapon explosion assets baked.");
        }

        private static Material Material(string name, Shader shader, string texture)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/{texture}.png"));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static PooledVFX Burst(string name, Material material, float seconds, float size, int rows)
        {
            var root = new GameObject(name);
            try
            {
                var particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.duration = 0.01f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = seconds;
                main.startSpeed = 0f;
                main.startSize = size;
                main.maxParticles = 1;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = particles.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var shape = particles.shape;
                shape.enabled = false;
                var sheet = particles.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 4;
                sheet.numTilesY = rows;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.maxParticleSize = 1f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend
                });
                root.AddComponent<PooledVFX>();
                return PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/Prefabs/{name}.prefab").GetComponent<PooledVFX>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
