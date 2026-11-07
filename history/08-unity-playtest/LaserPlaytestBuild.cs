using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Combat.Projectiles.Visual
{
    public static class LaserPlaytestBuild
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            var output = args[Array.IndexOf(args, "-laserPlaytestOutput") + 1];
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.None
            });
            Debug.Log($"LASER_PLAYTEST_BUILD={report.summary.result} errors={report.summary.totalErrors} output={output}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
