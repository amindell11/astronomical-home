using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class HangarMeasure
{
    public static async Task<object> Main()
    {
        var stage = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarPreviewStage>();
        if (!stage) throw new Exception("No preview stage");
        var timings = new FrameTiming[1];
        var deltas = new List<float>();
        var gpuMs = new List<double>();
        var textures = new HashSet<int>();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        var lastFrame = Time.frameCount;
        while (deltas.Count < 90 && elapsed.Elapsed.TotalSeconds < 15)
        {
            await Task.Delay(1);
            if (lastFrame == Time.frameCount) continue;
            lastFrame = Time.frameCount;
            deltas.Add(Time.unscaledDeltaTime * 1000f);
            textures.Add(stage.Texture.GetInstanceID());
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                gpuMs.Add(timings[0].gpuFrameTime);
        }
        var rt = stage.Texture as RenderTexture;
        var report = new {
            frames = deltas.Count,
            frameMsMedian = deltas.Count > 0 ? deltas.OrderBy(x => x).ElementAt(deltas.Count / 2) : 0,
            frameMsMean = deltas.Count > 0 ? deltas.Average() : 0,
            gpuMsMean = gpuMs.Count > 0 ? gpuMs.Average() : -1,
            gpuSamples = gpuMs.Count,
            textureInstances = textures.Count,
            texture = rt.width + "x" + rt.height + " @ " + rt.antiAliasing + "xMSAA",
            rtBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(rt),
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            vsync = QualitySettings.vSyncCount,
            targetFps = Application.targetFrameRate,
            cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(c => new { c.name, c.enabled, target = c.targetTexture ? c.targetTexture.name : "screen" }).ToArray()
        };
        System.IO.File.WriteAllText("D:/amind/git/agent-2/results/hangar_measure_last.txt", report.ToString());
        return report;
    }
}
