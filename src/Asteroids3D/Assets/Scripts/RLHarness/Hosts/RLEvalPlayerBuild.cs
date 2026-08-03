#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.RLHarness
{
    public static class RLEvalPlayerBuild
    {
        private const string Scene = "Assets/Scenes/RLHarnessEval.unity";

        public static void Build()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var outDir = Path.Combine(repoRoot, "build", "rl-harness");
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(outDir, "RLHarnessEval.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"[RLEvalPlayerBuild] result={summary.result} out={options.locationPathName} "
                + $"size={summary.totalSize} errors={summary.totalErrors} warnings={summary.totalWarnings}");

            var ok = summary.result == BuildResult.Succeeded;
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
            else if (!ok)
                throw new Exception($"Eval player build failed: {summary.result} ({summary.totalErrors} errors)");
        }
    }
}
#endif
