using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Keystones
{
    public static class PaintedSlipstreamBaker
    {
        private const string Root = "Assets/Visuals/Vfx/KeystoneTrails";
        private static readonly List<ParticleSystemVertexStream> Streams = new()
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.AnimBlend
        };

        public static void Bake()
        {
            var wake = Paint("IonWake", 0.7f);
            var crown = Paint("PressureCrown", 0.55f);
            Wire("Vanguard", wake, null);
            Wire("Bulwark", wake, crown);
            AssetDatabase.SaveAssets();
            Debug.Log("Painted slipstream bake complete.");
        }

        private static Material Paint(string name, float glow)
        {
            var path = $"{Root}/Textures/{name}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var materialPath = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Visuals/Vfx/WeaponExplosions/Materials/PaintedBurst.shader");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetFloat("_CoreGlow", glow);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Wire(string ship, Material wake, Material crown)
        {
            var path = $"Assets/Prefabs/Keystones/{ship}_Dash.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var slipstream = root.GetComponentInChildren<KeystoneSlipstream>(true);
                var trail = slipstream.transform;
                var serialized = new SerializedObject(slipstream);
                serialized.FindProperty("wake").objectReferenceValue = Configure(trail.Find("Streaks"), wake, 2);
                if (crown)
                {
                    serialized.FindProperty("crown").objectReferenceValue = Configure(trail.Find("Bow"), crown, 1);
                    trail.Find("Arcs").gameObject.SetActive(false);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {path}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static ParticleSystem Configure(Transform layer, Material paint, int capacity)
        {
            var ps = layer.GetComponent<ParticleSystem>();
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startRotation3D = true;
            main.startSize3D = true;
            main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission;
            emission.enabled = false;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = false;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            sheet.startFrame = 0f;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var renderer = layer.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = paint;
            renderer.pivot = Vector3.zero;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 1f;
            renderer.SetActiveVertexStreams(Streams);
            layer.gameObject.SetActive(true);
            return ps;
        }
    }
}
