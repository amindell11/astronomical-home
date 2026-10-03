using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildIllustratedPlayer
{
    public static string Build()
    {
        var output="D:/amind/git/astronomical-home/results/visual-playable/player/Astronomical.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName=output,
            target=BuildTarget.StandaloneWindows64,
            subtarget=(int)StandaloneBuildSubtarget.Player,
            options=BuildOptions.Development
        });
        var result=$"{report.summary.result}: errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}; bytes={report.summary.totalSize}; duration={report.summary.totalTime}; output={output}";
        File.WriteAllText("D:/amind/git/astronomical-home/results/visual-playable/build.txt",result);
        if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException(result);
        return result;
    }
}
