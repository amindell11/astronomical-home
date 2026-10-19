// SCRATCH diagnosis probe (hangar fps). Never commit.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public sealed class HangarPerfProbe : MonoBehaviour
{
    private static readonly FrameTiming[] Timing = new FrameTiming[1];
    public static string Status = "idle";

    private string outPath;
    private bool quit;
    private readonly List<Vector2Int> resolutions = new();
    private readonly List<int> qualities = new();
    private int warmFrames = 40;
    private int sampleFrames = 150;
    private bool passes = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        var path = Arg(args, "-hangarPerfProbe", null);
        if (path == null) return;
        Begin(path, true, Arg(args, "-probeRes", "1920x1080,3840x2160"), Arg(args, "-probeQuality", "0,2"),
            int.Parse(Arg(args, "-probeFrames", "150")));
    }

    private static string Arg(string[] args, string key, string fallback)
    {
        var i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    public static string Begin(string outPath, bool quit, string res, string quality, int frames)
    {
        var go = new GameObject("HangarPerfProbe");
        DontDestroyOnLoad(go);
        var p = go.AddComponent<HangarPerfProbe>();
        p.outPath = outPath;
        p.quit = quit;
        p.sampleFrames = frames;
        if (!string.IsNullOrEmpty(res))
            foreach (var r in res.Split(','))
            {
                var wh = r.Split('x');
                p.resolutions.Add(new Vector2Int(int.Parse(wh[0]), int.Parse(wh[1])));
            }
        if (!string.IsNullOrEmpty(quality))
            foreach (var q in quality.Split(','))
                p.qualities.Add(int.Parse(q));
        Status = "starting";
        return "probe started -> " + outPath;
    }

    private sealed class Ctx
    {
        public List<Renderer> sky = new();
        public Camera preview;
        public List<Canvas> hangarCanvases = new();
        public List<Canvas> allCanvases = new();
        public List<(Component data, PropertyInfo prop, bool original)> post = new();
        public object urp;
        public PropertyInfo renderScale;
        public PropertyInfo msaa;
    }

    private IEnumerator Start()
    {
        var deadline = Time.realtimeSinceStartup + 180f;
        Status = "waiting for hangar";
        while (!FindHangar() && Time.realtimeSinceStartup < deadline) yield return null;
        if (!FindHangar())
        {
            Write(new Dictionary<string, object> { { "error", "hangar never appeared" } });
            Finish();
            yield break;
        }
        yield return new WaitForSecondsRealtime(3f);

        Write(new Dictionary<string, object>
        {
            { "header", true },
            { "editor", Application.isEditor },
            { "device", SystemInfo.graphicsDeviceName },
            { "api", SystemInfo.graphicsDeviceType.ToString() },
            { "ftmEnabled", FrameTimingManager.IsFeatureEnabled() },
            { "gpuRecorder", SystemInfo.supportsGpuRecorder },
            { "hdrAvailable", HDROutputSettings.main.available },
            { "hdrActive", HDROutputSettings.main.active },
            { "refreshHz", Screen.currentResolution.refreshRateRatio.value },
            { "desktop", Screen.currentResolution.width + "x" + Screen.currentResolution.height },
        });

        var originalQuality = QualitySettings.GetQualityLevel();
        var resList = resolutions.Count > 0 ? resolutions : new List<Vector2Int> { new(-1, -1) };
        var qList = qualities.Count > 0 ? qualities : new List<int> { originalQuality };
        foreach (var res in resList)
        {
            if (res.x > 0)
            {
                var desktop = Screen.currentResolution;
                var mode = res.x >= desktop.width || res.y >= desktop.height
                    ? FullScreenMode.FullScreenWindow
                    : FullScreenMode.Windowed;
                Screen.SetResolution(res.x, res.y, mode);
                yield return new WaitForSecondsRealtime(2f);
            }
            foreach (var q in qList)
            {
                if (QualitySettings.GetQualityLevel() != q)
                {
                    QualitySettings.SetQualityLevel(q, true);
                    yield return new WaitForSecondsRealtime(1.5f);
                }
                yield return RunMatrix();
            }
        }
        if (QualitySettings.GetQualityLevel() != originalQuality)
            QualitySettings.SetQualityLevel(originalQuality, true);
        Finish();
    }

    private void Finish()
    {
        Status = "done";
        if (quit) Application.Quit();
        else Destroy(gameObject);
    }

    private static MonoBehaviour FindHangar() =>
        FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(m => m && m.isActiveAndEnabled && m.GetType().Name == "HangarScreen");

    private static Ctx Snapshot()
    {
        var ctx = new Ctx();
        var skyLayer = LayerMask.NameToLayer("Sky");
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.enabled && r.gameObject.activeInHierarchy && r.gameObject.layer == skyLayer)
                ctx.sky.Add(r);
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.name == "PreviewCamera" && c.enabled)
                ctx.preview = c;
        var hangar = FindHangar();
        if (hangar)
            ctx.hangarCanvases.AddRange(hangar.GetComponentsInChildren<Canvas>().Where(c => c.enabled));
        ctx.allCanvases.AddRange(FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled));
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (!c.enabled) continue;
            var data = c.GetComponent("UniversalAdditionalCameraData");
            if (!data) continue;
            var prop = data.GetType().GetProperty("renderPostProcessing");
            ctx.post.Add((data, prop, (bool)prop.GetValue(data)));
        }
        ctx.urp = GraphicsSettings.currentRenderPipeline;
        if (ctx.urp != null)
        {
            ctx.renderScale = ctx.urp.GetType().GetProperty("renderScale");
            ctx.msaa = ctx.urp.GetType().GetProperty("msaaSampleCount");
        }
        return ctx;
    }

    private IEnumerator RunMatrix()
    {
        var ctx = Snapshot();
        Write(new Dictionary<string, object>
        {
            { "matrix", QualitySettings.names[QualitySettings.GetQualityLevel()] },
            { "screen", Screen.width + "x" + Screen.height },
            { "sky", string.Join(" | ", ctx.sky.Select(Name)) },
            { "preview", ctx.preview ? ctx.preview.name : "none" },
            { "cameras", string.Join(" | ", FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.enabled)
                .Select(c => c.name + (c.targetTexture ? "->RT" : "") + " mask=" + c.cullingMask)) },
            { "canvases", string.Join(" | ", ctx.allCanvases.Select(c => c.name + ":" + c.renderMode)) },
        });

        yield return Measure("base_tierVsync", ctx);
        QualitySettings.vSyncCount = 0;
        yield return Measure("base", ctx);
        if (passes) yield return Passes(ctx);

        SetAll(ctx.sky, false);
        yield return Measure("no_sky", ctx);
        SetAll(ctx.sky, true);

        foreach (var r in ctx.sky)
        {
            r.enabled = false;
            yield return Measure("no_sky:" + Name(r), ctx);
            r.enabled = true;
        }
        yield return Measure("base_again", ctx);

        foreach (var r in ctx.sky)
        {
            SetAll(ctx.sky, false);
            r.enabled = true;
            yield return Measure("only_sky:" + Name(r), ctx);
        }
        SetAll(ctx.sky, true);

        if (ctx.preview)
        {
            ctx.preview.enabled = false;
            yield return Measure("no_preview", ctx);
            ctx.preview.enabled = true;
        }

        SetCanvases(ctx.hangarCanvases, false);
        yield return Measure("no_hangar_ui", ctx);
        SetCanvases(ctx.hangarCanvases, true);

        SetCanvases(ctx.allCanvases, false);
        yield return Measure("no_any_ui", ctx);
        SetCanvases(ctx.allCanvases, true);

        SetPost(ctx, false);
        yield return Measure("no_post", ctx);
        SetPost(ctx, true);

        if (ctx.renderScale != null)
        {
            var original = (float)ctx.renderScale.GetValue(ctx.urp);
            if (Math.Abs(original - 1f) > 0.001f)
            {
                ctx.renderScale.SetValue(ctx.urp, 1f);
                yield return Measure("renderScale_1", ctx);
                ctx.renderScale.SetValue(ctx.urp, original);
            }
            ctx.renderScale.SetValue(ctx.urp, 0.5f);
            yield return Measure("renderScale_0.5", ctx);
            ctx.renderScale.SetValue(ctx.urp, original);
        }

        if (ctx.msaa != null)
        {
            var original = (int)ctx.msaa.GetValue(ctx.urp);
            if (original > 1)
            {
                ctx.msaa.SetValue(ctx.urp, 1);
                yield return Measure("msaa_1", ctx);
                ctx.msaa.SetValue(ctx.urp, original);
            }
        }

        SetAll(ctx.sky, false);
        if (ctx.preview) ctx.preview.enabled = false;
        SetCanvases(ctx.allCanvases, false);
        SetPost(ctx, false);
        yield return Measure("floor_all_off", ctx);
        SetAll(ctx.sky, true);
        yield return Measure("sky_only_no_post", ctx);
        SetPost(ctx, true);
        yield return Measure("sky_only_with_post", ctx);
        if (ctx.preview) ctx.preview.enabled = true;
        SetCanvases(ctx.allCanvases, true);
        yield return Measure("base_end", ctx);
    }

    private static string Name(Renderer r) =>
        r.name + "/" + (r.sharedMaterial ? r.sharedMaterial.name + "<" + r.sharedMaterial.shader.name + ">" : "null");

    private static void SetAll(List<Renderer> rs, bool on)
    {
        foreach (var r in rs) if (r) r.enabled = on;
    }

    private static void SetCanvases(List<Canvas> cs, bool on)
    {
        foreach (var c in cs) if (c) c.enabled = on;
    }

    private static void SetPost(Ctx ctx, bool restore)
    {
        foreach (var (data, prop, original) in ctx.post)
            if (data) prop.SetValue(data, restore && original);
    }

    private IEnumerator Measure(string label, Ctx ctx)
    {
        Status = "measuring " + label;
        for (var i = 0; i < warmFrames; i++) yield return null;
        var gpu = new List<double>();
        var main = new List<double>();
        var render = new List<double>();
        var cpu = new List<double>();
        var presentWait = new List<double>();
        var dt = new List<double>();
        ulong last = 0;
        var frames = 0;
        while (gpu.Count < sampleFrames && frames < sampleFrames * 3)
        {
            yield return null;
            frames++;
            dt.Add(Time.unscaledDeltaTime * 1000.0);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, Timing) == 0) continue;
            var t = Timing[0];
            if (t.frameStartTimestamp == last) continue;
            last = t.frameStartTimestamp;
            gpu.Add(t.gpuFrameTime);
            main.Add(t.cpuMainThreadFrameTime);
            render.Add(t.cpuRenderThreadFrameTime);
            cpu.Add(t.cpuFrameTime);
            presentWait.Add(t.cpuMainThreadPresentWaitTime);
        }

        var rt = ctx.preview && ctx.preview.targetTexture ? ctx.preview.targetTexture : null;
        Write(new Dictionary<string, object>
        {
            { "label", label },
            { "quality", QualitySettings.names[QualitySettings.GetQualityLevel()] },
            { "screen", Screen.width + "x" + Screen.height },
            { "vsync", QualitySettings.vSyncCount },
            { "renderScale", ctx.renderScale?.GetValue(ctx.urp) ?? -1f },
            { "msaa", ctx.msaa?.GetValue(ctx.urp) ?? -1 },
            { "previewRT", rt ? rt.width + "x" + rt.height + "x" + rt.antiAliasing : "none" },
            { "n", gpu.Count },
            { "gpu_med", Median(gpu) },
            { "gpu_p90", Pct(gpu, 0.9) },
            { "main_med", Median(main) },
            { "render_med", Median(render) },
            { "cpuFrame_med", Median(cpu) },
            { "presentWait_med", Median(presentWait) },
            { "dt_med", Median(dt) },
            { "fps_med", 1000.0 / Math.Max(0.001, Median(dt)) },
        });
    }

    private IEnumerator Passes(Ctx ctx)
    {
        Status = "passes";
        if (!SystemInfo.supportsGpuRecorder)
        {
            Write(new Dictionary<string, object> { { "passes", "unsupported" } });
            yield break;
        }
        var names = new List<string>();
        Sampler.GetNames(names);
        var recs = new List<(string name, Recorder rec)>();
        foreach (var n in names.Distinct())
        {
            var rec = Recorder.Get(n);
            if (rec == null || !rec.isValid) continue;
            rec.enabled = true;
            recs.Add((n, rec));
        }
        for (var i = 0; i < 15; i++) yield return null;
        var sums = new double[recs.Count];
        var counts = new double[recs.Count];
        const int frames = 90;
        for (var f = 0; f < frames; f++)
        {
            yield return null;
            for (var i = 0; i < recs.Count; i++)
            {
                sums[i] += recs[i].rec.gpuElapsedNanoseconds;
                counts[i] += recs[i].rec.gpuSampleBlockCount;
            }
        }
        foreach (var r in recs) r.rec.enabled = false;
        var top = Enumerable.Range(0, recs.Count)
            .Where(i => sums[i] > 0)
            .OrderByDescending(i => sums[i])
            .Take(45)
            .Select(i => recs[i].name + "=" + (sums[i] / frames / 1e6).ToString("0.000", CultureInfo.InvariantCulture) +
                         "ms x" + (counts[i] / frames).ToString("0.#", CultureInfo.InvariantCulture));
        Write(new Dictionary<string, object>
        {
            { "passes", string.Join(" ; ", top) },
            { "quality", QualitySettings.names[QualitySettings.GetQualityLevel()] },
            { "screen", Screen.width + "x" + Screen.height },
        });
    }

    private static double Median(List<double> xs) => Pct(xs, 0.5);

    private static double Pct(List<double> xs, double p)
    {
        if (xs.Count == 0) return -1;
        var s = xs.OrderBy(x => x).ToList();
        return s[Math.Min(s.Count - 1, (int)(p * (s.Count - 1) + 0.5))];
    }

    private void Write(Dictionary<string, object> row)
    {
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var kv in row)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(kv.Key).Append("\":");
            switch (kv.Value)
            {
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: sb.Append(d.ToString("0.###", CultureInfo.InvariantCulture)); break;
                case float fl: sb.Append(fl.ToString("0.###", CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n); break;
                default: sb.Append('"').Append((kv.Value ?? "").ToString().Replace("\\", "/").Replace("\"", "'")).Append('"'); break;
            }
        }
        sb.Append("}\n");
        File.AppendAllText(outPath, sb.ToString());
    }
}
