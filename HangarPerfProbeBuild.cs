// SCRATCH diagnosis build hook (hangar fps). Never commit.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class HangarPerfProbeBuild
{
    public static void Build()
    {
        var args = Environment.GetCommandLineArgs();
        var outDir = args[Array.IndexOf(args, "-probeOut") + 1];
        PlayerSettings.enableFrameTimingStats = true;
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Debug.Log("PROBE_BUILD_SCENES " + string.Join(",", scenes));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outDir, "Astronomical.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        });
        Debug.Log("PROBE_BUILD_RESULT " + report.summary.result + " time=" + report.summary.totalTime +
                  " errors=" + report.summary.totalErrors);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
