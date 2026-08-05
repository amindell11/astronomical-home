#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>-executeMethod entry that builds the headless StandaloneWindows64 training player (the RLTraining scene only), so ML-Agents can launch it with --env. Exits nonzero on any build error.</summary>
    public static class RLTrainingPlayerBuild
    {
        public const string Scene = "Assets/Scenes/RLTraining.unity";

        public static void Build()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var outDir = Path.Combine(repoRoot, "build", "rl-training");
            Directory.CreateDirectory(outDir);

            // Training builds pay for profiler instrumentation and full Debug.Log stack traces on
            // every fixed step; RL_BUILD_DEVELOPMENT=1 buys them back for a profiling session.
            var development = Environment.GetEnvironmentVariable("RL_BUILD_DEVELOPMENT") == "1";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(outDir, "RLTraining.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"[RLTrainingPlayerBuild] result={summary.result} out={options.locationPathName} "
                + $"development={development} size={summary.totalSize} errors={summary.totalErrors} warnings={summary.totalWarnings}");

            var ok = summary.result == BuildResult.Succeeded;
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
            else if (!ok)
                throw new Exception($"RLTraining player build failed: {summary.result} ({summary.totalErrors} errors)");
        }
    }
}
#endif
