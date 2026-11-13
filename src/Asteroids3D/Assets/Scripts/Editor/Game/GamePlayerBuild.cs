using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Builds the StandaloneWindows64 player from Build Settings scenes and project-wide Player Settings.
    /// An active custom build profile is switched to the platform profile first, and stays switched:
    /// its player-settings copy would replace those settings.
    /// scripts/build_game_player.ps1 is the front door and names the output folder.
    /// </summary>
    public static class GamePlayerBuild
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            string outDir = null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-gameBuildDir")
                    outDir = args[i + 1];
            }

            if (string.IsNullOrWhiteSpace(outDir))
                throw new ArgumentException("-gameBuildDir <dir> is required. Run scripts/build_game_player.ps1.");

            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile != null)
            {
                BuildProfile.SetActiveBuildProfile(null);
                Debug.Log($"[GamePlayerBuild] switched the active build profile from '{activeProfile.name}' to the platform profile");
            }

            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                locationPathName = Path.Combine(outDir, PlayerSettings.productName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"[GamePlayerBuild] result={summary.result} out={options.locationPathName} size={summary.totalSize} errors={summary.totalErrors} warnings={summary.totalWarnings}");
            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"Game player build failed: result={summary.result} errors={summary.totalErrors}");
        }
    }
}
