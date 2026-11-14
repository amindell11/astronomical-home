#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AI;
using Capture.Recording;
using Combat.Weapons;
using Ships;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RL.Arena;
using Ships.Loadout;

namespace RL.Hosts
{
    /// <summary>-executeMethod entry point for a batch-mode harness run, plus the item-catalog resolvers its parse reads.</summary>
    public static class TrainingBootstrap
    {
        private const string GameViewCaptureType =
            "Capture.GameView.GameViewEpisodeCapture, Capture.GameView.Editor";

        /// <summary>Harness-run batch entry: the RL_HARNESS_* environment (grammar: <see cref="HarnessSpec.Parse"/>) parses HERE so a malformed value fails before play mode; HarnessHost exits the editor with code 0 when the lane's summary artifact is written.</summary>
        public static void RunHarness()
        {
            var spec = HarnessSpec.Parse(Environment.GetEnvironmentVariable, CatalogWeapons, CatalogChassis,
                CatalogPilots, CatalogShields,
                () => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("[HarnessHost]").AddComponent<HarnessHost>();
            host.assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            host.spec = spec;
            host.exitEditorWhenComplete = true;
            // Every filmed episode goes through the Game View, gizmo profile or not.
            if (spec.record.enabled) AttachGameViewCapture(host);
            EditorApplication.EnterPlaymode();
        }

        private static void AttachGameViewCapture(HarnessHost host)
        {
            var type = Type.GetType(GameViewCaptureType, throwOnError: true);
            if (!typeof(ScriptableObject).IsAssignableFrom(type) || !typeof(IEpisodeCapture).IsAssignableFrom(type))
                throw new InvalidOperationException($"{GameViewCaptureType} does not implement IEpisodeCapture.");
            var module = ScriptableObject.CreateInstance(type);
            module.hideFlags = HideFlags.HideAndDontSave;
            host.captureModule = module;
        }

        internal static IReadOnlyList<WeaponComponent> CatalogWeapons() =>
            AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath).Weapons;

        internal static IReadOnlyList<Ship> CatalogChassis() =>
            AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath).Chassis;

        internal static IReadOnlyList<AICommander> CatalogPilots()
        {
            var pilots = new List<AICommander>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Pilots" }))
            {
                var pilot = AssetDatabase.LoadAssetAtPath<AICommander>(AssetDatabase.GUIDToAssetPath(guid));
                if (pilot) pilots.Add(pilot);
            }
            pilots.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return pilots;
        }

        internal static IReadOnlyList<ShieldModule> CatalogShields()
        {
            var shields = new List<ShieldModule>();
            foreach (var guid in AssetDatabase.FindAssets("t:ShieldModule", new[] { "Assets/Settings/Ships/Shields" }))
            {
                var shield = AssetDatabase.LoadAssetAtPath<ShieldModule>(AssetDatabase.GUIDToAssetPath(guid));
                if (shield) shields.Add(shield);
            }
            shields.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return shields;
        }
    }
}
#endif
