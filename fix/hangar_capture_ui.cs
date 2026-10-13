using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class HangarCaptureUi
{
    public static async Task<object> Main()
    {
        if (!UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarScreen>())
            throw new Exception("No live hangar to capture");
        const string path = "D:/amind/git/agent-2/results/hangar_fixed_ui.png";
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        await Task.Delay(200);
        ScreenCapture.CaptureScreenshot(path);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!File.Exists(path) && watch.Elapsed.TotalSeconds < 10)
            await Task.Delay(50);
        if (!File.Exists(path)) throw new Exception("Composite screenshot was not written");
        return new { path, bytes = new FileInfo(path).Length };
    }
}
