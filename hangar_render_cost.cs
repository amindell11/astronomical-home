using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class HangarRenderCost
{
    public static object Main()
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarScreen>();
        var stage = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarPreviewStage>();
        var camera = (Camera)typeof(UI.Hangar.HangarPreviewStage).GetField("stageCamera", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stage);
        var wasEnabled = screen.enabled;
        var priorTarget = RenderTexture.active;
        var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        var rows = new List<object>();
        screen.enabled = false;
        try
        {
            var widths = new[] { 768, 768, 1601, 2304, 2560, 2304, 1601, 768, 768 };
            var samples = new[] { 1, 8, 8, 8, 8, 8, 8, 8, 1 };
            for (var j = 0; j < widths.Length; j++)
            {
                RenderTexture.active = null;
                stage.Resize(widths[j]);
                var rt = (RenderTexture)stage.Texture;
                rt.Release();
                rt.antiAliasing = samples[j];
                rt.Create();
                var times = new List<double>();
                for (var i = 0; i < 7; i++)
                {
                    var timer = Stopwatch.StartNew();
                    camera.Render();
                    RenderTexture.active = rt;
                    pixel.ReadPixels(new Rect(rt.width / 2, rt.height / 2, 1, 1), 0, 0, false);
                    timer.Stop();
                    if (i > 1) times.Add(timer.Elapsed.TotalMilliseconds);
                }
                rows.Add(new { width = rt.width, msaa = rt.antiAliasing, depth = rt.depthStencilFormat.ToString(), centerPixel = pixel.GetPixel(0, 0).ToString(), bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(rt), samplesMs = times, medianMs = times.OrderBy(x => x).ElementAt(times.Count / 2) });
            }
        }
        finally
        {
            RenderTexture.active = priorTarget;
            UnityEngine.Object.DestroyImmediate(pixel);
            screen.enabled = wasEnabled;
        }
        return new { note = "Preview camera Render plus synchronous one-pixel readback; same ship pose and frame. Timings include CPU and GPU wait, not standalone GPU duration.", rows };
    }
}
