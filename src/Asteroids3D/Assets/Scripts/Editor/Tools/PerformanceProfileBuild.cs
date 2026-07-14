using System;
using System.IO;
using Game.Bootstrap;
using Game.Diagnostics;
using Ships;
using Ships.Command;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tools.Editor
{
    public static class PerformanceProfileBuild
    {
        private const string SourceScene = "Assets/Scenes/InitScene.unity";
        private const string ProfileScene = "Assets/Scenes/__PerformanceProfile.unity";
        private const string EnvironmentScene = "Assets/Scenes/Environments/Environment_1.unity";
        private const string PipelineAsset = "Assets/Settings/Rendering/URP-Balanced.asset";
        private const string ProjectSettingsAsset = "ProjectSettings/ProjectSettings.asset";
        private const string StressShipPrefab = "Assets/Prefabs/Ships/Ship_2.prefab";
        private const string StressCommanderPrefab = "Assets/Prefabs/Pilots/UtilityPilot.prefab";

        public static void Build()
        {
            var outputPath = Environment.GetEnvironmentVariable("ASTRO_PROFILE_BUILD");
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new InvalidOperationException("ASTRO_PROFILE_BUILD must name the profiling player executable.");

            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
            var pipelineAsset = File.ReadAllText(PipelineAsset);
            var projectSettingsAsset = File.ReadAllText(ProjectSettingsAsset);
            var resourcesExisted = AssetDatabase.IsValidFolder("Assets/Resources");
            AssetDatabase.DeleteAsset(ProfileScene);
            if (!AssetDatabase.CopyAsset(SourceScene, ProfileScene))
                throw new InvalidOperationException($"Could not copy {SourceScene} to {ProfileScene}.");

            try
            {
                var scene = EditorSceneManager.OpenScene(ProfileScene, OpenSceneMode.Single);
                var driver = UnityEngine.Object.FindFirstObjectByType<GameDriver>();
                if (!driver)
                    throw new InvalidOperationException($"{ProfileScene} has no GameDriver.");

                var serializedDriver = new SerializedObject(driver);
                serializedDriver.FindProperty("hangarScreenPrefab").objectReferenceValue = null;
                serializedDriver.ApplyModifiedPropertiesWithoutUndo();
                var capture = driver.gameObject.AddComponent<PerformanceProfileCapture>();
                var serializedCapture = new SerializedObject(capture);
                serializedCapture.FindProperty("stressShipTemplate").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(StressShipPrefab).GetComponent<Ship>();
                serializedCapture.FindProperty("stressCommanderTemplate").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(StressCommanderPrefab).GetComponent<Commander>();
                serializedCapture.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProfileScene, EnvironmentScene },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Profiling player build failed: {report.summary.result}.");
            }
            finally
            {
                EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
                AssetDatabase.DeleteAsset(ProfileScene);
                File.WriteAllText(PipelineAsset, pipelineAsset);
                File.WriteAllText(ProjectSettingsAsset, projectSettingsAsset);
                AssetDatabase.DeleteAsset("Assets/Resources/PerformanceTestRunInfo.json");
                AssetDatabase.DeleteAsset("Assets/Resources/PerformanceTestRunSettings.json");
                if (!resourcesExisted)
                    AssetDatabase.DeleteAsset("Assets/Resources");
            }
        }
    }
}
