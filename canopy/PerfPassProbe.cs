// SCRATCH perf-pass probe (sibling of FlightPerfProbe on evidence/hangar-fps). Never commit.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed partial class PerfPassProbe : MonoBehaviour
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FrameTiming[] Timing = new FrameTiming[1];

    private string outPath;
    private float settleSeconds = 15f;
    private float liveSeconds = 30f;
    private float profiledSeconds = 30f;
    private float hangarSeconds = 10f;
    private readonly List<Vector2Int> resolutions = new();
    private int quality = 2;
    private int warmFrames = 40;
    private int sampleFrames = 150;
    private bool skipFrozen;
    private bool canopy;
    private bool inkLite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        var path = Arg(args, "-perfPassProbe", null);
        if (path == null) return;
        var go = new GameObject("PerfPassProbe");
        DontDestroyOnLoad(go);
        var p = go.AddComponent<PerfPassProbe>();
        p.outPath = path;
        p.settleSeconds = F(Arg(args, "-probeSettle", "15"));
        p.liveSeconds = F(Arg(args, "-probeLive", "30"));
        p.profiledSeconds = F(Arg(args, "-probeProfiled", "30"));
        p.hangarSeconds = F(Arg(args, "-probeHangar", "10"));
        p.quality = int.Parse(Arg(args, "-probeQuality", "2"));
        p.skipFrozen = Array.IndexOf(args, "-probeSkipFrozen") >= 0;
        p.canopy = Array.IndexOf(args, "-probeCanopy") >= 0;
        p.inkLite = Array.IndexOf(args, "-probeInkLite") >= 0;
        if (p.inkLite) p.canopy = true;
        foreach (var r in Arg(args, "-probeRes", "1920x1080,3840x2160").Split(','))
        {
            var wh = r.Split('x');
            p.resolutions.Add(new Vector2Int(int.Parse(wh[0]), int.Parse(wh[1])));
        }
    }

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    private static string Arg(string[] args, string key, string fallback)
    {
        var i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private sealed class Ctx
    {
        public List<Renderer> sky = new();
        public Camera main;
        public List<Camera> otherCams = new();
        public List<Canvas> canvases = new();
        public List<(Component data, PropertyInfo prop, bool original)> post = new();
        public List<(object feature, string name)> features = new();
        public object urp;
    }

    private IEnumerator Start()
    {
        var deadline = Time.realtimeSinceStartup + 240f;
        while (!Find("HangarScreen") && Time.realtimeSinceStartup < deadline) yield return null;
        var hangar = Find("HangarScreen");
        if (!hangar)
        {
            Fail("hangar never appeared");
            yield break;
        }
        Write(new Dictionary<string, object>
        {
            { "header", true },
            { "device", SystemInfo.graphicsDeviceName },
            { "api", SystemInfo.graphicsDeviceType.ToString() },
            { "cpu", SystemInfo.processorType },
            { "ftmEnabled", FrameTimingManager.IsFeatureEnabled() },
            { "gpuRecorder", SystemInfo.supportsGpuRecorder },
            { "refreshHz", Screen.currentResolution.refreshRateRatio.value },
            { "desktop", Screen.currentResolution.width + "x" + Screen.currentResolution.height },
            { "bootSeconds", Time.realtimeSinceStartup },
        });
        yield return new WaitForSecondsRealtime(3f);
        yield return SetQuality(quality);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        foreach (var res in canopy ? new List<Vector2Int>() : resolutions)
        {
            yield return SetResolution(res);
            QualitySettings.vSyncCount = 0;
            yield return Live("hangar_live", hangarSeconds, null);
            if (!skipFrozen)
            {
                var ctx = Snapshot("DriftCam");
                yield return Measure("hangar_base", ctx);
                SetAll(ctx.sky, false);
                yield return Measure("hangar_no_sky", ctx);
                SetAll(ctx.sky, true);
                yield return Features(ctx, "hangar_");
            }
        }
        yield return SetResolution(resolutions[0]);

        var host = Find("GameHost");
        var button = Get(hangar, "launchButton");
        var onClick = button.GetType().GetProperty("onClick").GetValue(button);
        onClick.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(onClick, null);

        // The load itself: frame pacing from the press to the first flight frame.
        var loadDt = new List<double>();
        deadline = Time.realtimeSinceStartup + 240f;
        while (!InFlight(host) && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            loadDt.Add(Time.unscaledDeltaTime * 1000.0);
        }
        if (!InFlight(host))
        {
            Fail("flight never started; active scene " + SceneManager.GetActiveScene().name);
            yield break;
        }
        var damage = Get(Get(host, "rigInstance"), "Player") is Component player ? Get(player, "Damage") : null;
        damage?.GetType().GetMethod("SetInvulnerability").Invoke(damage, new object[] { 36000f });
        Write(new Dictionary<string, object>
        {
            { "flight", true },
            { "sector", Get(host, "ActiveSector") is UnityEngine.Object s && s ? s.name : "null" },
            { "invulnerable", damage != null },
            { "loadFrames", loadDt.Count },
            { "loadSeconds", loadDt.Sum() / 1000.0 },
            { "loadMaxDt", loadDt.Count > 0 ? loadDt.Max() : -1 },
            { "loadTop", string.Join(" ", loadDt.OrderByDescending(x => x).Take(8).Select(x => x.ToString("0.#", CultureInfo.InvariantCulture))) },
        });

        // Arrival: first seconds in a fresh sector, where first-use costs land.
        yield return Live("arrival", 6f, null);
        var remaining = settleSeconds - 6f;
        if (remaining > 0) yield return new WaitForSecondsRealtime(remaining);

        if (canopy)
        {
            yield return inkLite ? InkLite() : Canopy();
            Write(new Dictionary<string, object> { { "done", true }, { "seconds", Time.realtimeSinceStartup } });
            Application.Quit();
            yield break;
        }

        foreach (var res in resolutions)
        {
            yield return SetResolution(res);
            QualitySettings.vSyncCount = 0;
            yield return Live("flight_live", liveSeconds, null);
        }

        yield return SetResolution(resolutions[0]);
        var raw = Path.ChangeExtension(outPath, null) + "_profile";
        yield return Live("flight_profiled", profiledSeconds, raw);

        if (!skipFrozen)
        {
            Time.timeScale = 0.001f;
            yield return new WaitForSecondsRealtime(1f);
            foreach (var res in resolutions)
            {
                yield return SetResolution(res);
                QualitySettings.vSyncCount = 0;
                yield return Frozen();
            }
            Time.timeScale = 1f;
        }

        Write(new Dictionary<string, object> { { "done", true }, { "seconds", Time.realtimeSinceStartup } });
        Application.Quit();
    }

    private IEnumerator Live(string label, float seconds, string profilePath)
    {
        for (var i = 0; i < 20; i++) yield return null;
        var before = Census();
        var gc0 = GC.CollectionCount(0);
        using var gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        if (profilePath != null)
        {
            Profiler.maxUsedMemory = 512 * 1024 * 1024;
            Profiler.logFile = profilePath;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
        }
        var dt = new List<double>();
        var main = new List<double>();
        var render = new List<double>();
        var gpu = new List<double>();
        var alloc = new List<double>();
        var stamps = new List<double>();
        var start = Time.realtimeSinceStartup;
        ulong last = 0;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
            dt.Add(Time.unscaledDeltaTime * 1000.0);
            stamps.Add(Time.realtimeSinceStartup - start);
            alloc.Add(gcAlloc.Valid ? gcAlloc.LastValue / 1024.0 : -1);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, Timing) == 0) continue;
            var t = Timing[0];
            if (t.frameStartTimestamp == last) continue;
            last = t.frameStartTimestamp;
            main.Add(t.cpuMainThreadFrameTime);
            render.Add(t.cpuRenderThreadFrameTime);
            gpu.Add(t.gpuFrameTime);
        }
        if (profilePath != null)
        {
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = "";
        }
        var worst = Enumerable.Range(0, dt.Count).OrderByDescending(i => dt[i]).Take(12)
            .Select(i => stamps[i].ToString("0.00", CultureInfo.InvariantCulture) + "s:" +
                         dt[i].ToString("0.0", CultureInfo.InvariantCulture) + "ms/gc" +
                         alloc[i].ToString("0", CultureInfo.InvariantCulture) + "k");
        var row = new Dictionary<string, object>
        {
            { "live", label },
            { "profiled", profilePath != null },
            { "screen", Screen.width + "x" + Screen.height },
            { "quality", QualitySettings.names[QualitySettings.GetQualityLevel()] },
            { "seconds", seconds },
            { "frames", dt.Count },
            { "dt_med", Pct(dt, 0.5) },
            { "dt_p95", Pct(dt, 0.95) },
            { "dt_p99", Pct(dt, 0.99) },
            { "dt_max", dt.Count > 0 ? dt.Max() : -1 },
            { "over16", dt.Count(x => x > 16.67) },
            { "over25", dt.Count(x => x > 25) },
            { "over33", dt.Count(x => x > 33.3) },
            { "over50", dt.Count(x => x > 50) },
            { "main_med", Pct(main, 0.5) },
            { "main_p95", Pct(main, 0.95) },
            { "main_p99", Pct(main, 0.99) },
            { "render_med", Pct(render, 0.5) },
            { "render_p95", Pct(render, 0.95) },
            { "gpu_med", Pct(gpu, 0.5) },
            { "gpu_p95", Pct(gpu, 0.95) },
            { "gpu_p99", Pct(gpu, 0.99) },
            { "gcKB_med", Pct(alloc, 0.5) },
            { "gcKB_p95", Pct(alloc, 0.95) },
            { "gcKB_total", alloc.Where(x => x > 0).Sum() },
            { "gcCollections", GC.CollectionCount(0) - gc0 },
            { "worst", string.Join(" ", worst) },
            { "censusBefore", before },
            { "censusAfter", Census() },
        };
        Write(row);
        File.WriteAllText(Path.ChangeExtension(outPath, null) + "_" + label + "_" + Screen.width + ".dt.txt",
            string.Join("\n", dt.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture))));
    }

    private IEnumerator Frozen()
    {
        var ctx = Snapshot("ObserverCam");
        Write(new Dictionary<string, object>
        {
            { "matrix", QualitySettings.names[QualitySettings.GetQualityLevel()] },
            { "screen", Screen.width + "x" + Screen.height },
            { "sky", string.Join(" | ", ctx.sky.Select(Name)) },
            { "cameras", string.Join(" | ", FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.enabled)
                .Select(c => c.name + (c.targetTexture ? "->RT" : "") + " fmt=" +
                             (c.targetTexture ? c.targetTexture.graphicsFormat.ToString() : "bb"))) },
            { "features", string.Join(" | ", ctx.features.Select(f => f.name)) },
            { "census", Census() },
        });
        ScreenCapture.CaptureScreenshot(Path.ChangeExtension(outPath, null) + "_frozen_" + Screen.width + ".png");
        for (var i = 0; i < 5; i++) yield return null;

        yield return Measure("base", ctx);
        yield return Passes();
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
        foreach (var c in ctx.otherCams)
        {
            c.enabled = false;
            yield return Measure("no_cam:" + c.name, ctx);
            c.enabled = true;
        }
        yield return Features(ctx, "");
        SetCanvases(ctx.canvases, false);
        yield return Measure("no_any_ui", ctx);
        SetCanvases(ctx.canvases, true);
        SetPost(ctx, false);
        yield return Measure("no_post", ctx);
        SetPost(ctx, true);
        SetAll(ctx.sky, false);
        SetCanvases(ctx.canvases, false);
        SetPost(ctx, false);
        yield return Measure("floor_sky_ui_post_off", ctx);
        SetAll(ctx.sky, true);
        SetPost(ctx, true);
        SetCanvases(ctx.canvases, true);
        yield return Measure("base_end", ctx);
    }

    private IEnumerator Features(Ctx ctx, string prefix)
    {
        foreach (var (feature, name) in ctx.features)
        {
            var isActive = (bool)feature.GetType().GetProperty("isActive").GetValue(feature);
            if (!isActive) continue;
            var setActive = feature.GetType().GetMethod("SetActive");
            setActive.Invoke(feature, new object[] { false });
            yield return Measure(prefix + "no_feature:" + name, ctx);
            setActive.Invoke(feature, new object[] { true });
        }
    }

    private static string Census()
    {
        var mbs = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int Count(string type) => mbs.Count(m => m && m.isActiveAndEnabled && IsA(m.GetType(), type));
        return "damageables=" + Count("DamageController") +
               " projectiles=" + Count("ProjectileBase") +
               " bodies=" + FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Count(r => r && r.gameObject.activeInHierarchy) +
               " particles=" + FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(ps => ps.isPlaying) +
               " visibleRenderers=" + FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.isVisible) +
               " canvases=" + FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled) +
               " cams=" + FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled);
    }

    private static bool IsA(Type t, string name)
    {
        for (; t != null; t = t.BaseType)
            if (t.Name == name) return true;
        return false;
    }

    private static IEnumerator SetResolution(Vector2Int res)
    {
        var desktop = Screen.currentResolution;
        var mode = res.x >= desktop.width || res.y >= desktop.height
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
        if (Screen.width == res.x && Screen.height == res.y && Screen.fullScreenMode == mode) yield break;
        Screen.SetResolution(res.x, res.y, mode);
        yield return new WaitForSecondsRealtime(2f);
    }

    private static IEnumerator SetQuality(int q)
    {
        if (QualitySettings.GetQualityLevel() == q) yield break;
        QualitySettings.SetQualityLevel(q, true);
        yield return new WaitForSecondsRealtime(1.5f);
    }

    private void Fail(string error)
    {
        Write(new Dictionary<string, object> { { "error", error } });
        Application.Quit(1);
    }

    private static bool InFlight(MonoBehaviour host)
    {
        if (Find("HangarScreen")) return false;
        if (Get(host, "ActiveSector") is not UnityEngine.Object sector || !sector) return false;
        var splash = Find("LoadingSplash");
        if (splash && splash.GetComponent<Canvas>() is Canvas c && c.enabled) return false;
        var observer = Find("ObserverCam");
        return observer && observer.GetComponent<Camera>().enabled;
    }

    private static object Get(object target, string member)
    {
        if (target == null) return null;
        var type = target.GetType();
        for (var t = type; t != null; t = t.BaseType)
        {
            var field = t.GetField(member, Any);
            if (field != null) return field.GetValue(target);
            var prop = t.GetProperty(member, Any);
            if (prop != null) return prop.GetValue(target);
        }
        throw new MissingMemberException(type.Name, member);
    }

    private static MonoBehaviour Find(string typeName) =>
        FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(m => m && m.isActiveAndEnabled && m.GetType().Name == typeName);

    private static Ctx Snapshot(string mainCamType)
    {
        var ctx = new Ctx();
        var skyLayer = LayerMask.NameToLayer("Sky");
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.enabled && r.gameObject.activeInHierarchy && r.gameObject.layer == skyLayer)
                ctx.sky.Add(r);
        var mainCam = Find(mainCamType);
        ctx.main = mainCam ? mainCam.GetComponent<Camera>() : null;
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.enabled && c != ctx.main)
                ctx.otherCams.Add(c);
        ctx.canvases.AddRange(FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled && c.isRootCanvas));
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
            var list = ctx.urp.GetType().GetField("m_RendererDataList", Any)?.GetValue(ctx.urp) as Array;
            var seen = new HashSet<object>();
            if (list != null)
                foreach (var data in list)
                {
                    if (data == null) continue;
                    var feats = data.GetType().GetProperty("rendererFeatures")?.GetValue(data) as IEnumerable;
                    if (feats == null) continue;
                    foreach (var f in feats)
                        if (f != null && seen.Add(f))
                            ctx.features.Add((f, ((UnityEngine.Object)data).name + "/" + ((UnityEngine.Object)f).name + "<" + f.GetType().Name + ">"));
                }
        }
        return ctx;
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
        for (var i = 0; i < warmFrames; i++) yield return null;
        var gpu = new List<double>();
        var main = new List<double>();
        var render = new List<double>();
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
        }
        Write(new Dictionary<string, object>
        {
            { "label", label },
            { "quality", QualitySettings.names[QualitySettings.GetQualityLevel()] },
            { "screen", Screen.width + "x" + Screen.height },
            { "n", gpu.Count },
            { "gpu_med", Pct(gpu, 0.5) },
            { "gpu_p90", Pct(gpu, 0.9) },
            { "main_med", Pct(main, 0.5) },
            { "render_med", Pct(render, 0.5) },
            { "dt_med", Pct(dt, 0.5) },
        });
    }

    private IEnumerator Passes(string label = "")
    {
        if (!SystemInfo.supportsGpuRecorder) yield break;
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
            .Take(50)
            .Select(i => recs[i].name + "=" + (sums[i] / frames / 1e6).ToString("0.000", CultureInfo.InvariantCulture) +
                         "ms x" + (counts[i] / frames).ToString("0.#", CultureInfo.InvariantCulture));
        var canopyPasses = Enumerable.Range(0, recs.Count)
            .Where(i => recs[i].name.StartsWith("Canopy"))
            .Select(i => recs[i].name + "=" + (sums[i] / frames / 1e6).ToString("0.000", CultureInfo.InvariantCulture) +
                         "ms x" + (counts[i] / frames).ToString("0.#", CultureInfo.InvariantCulture));
        Write(new Dictionary<string, object>
        {
            { "passesFor", label },
            { "canopyPasses", string.Join(" ; ", canopyPasses) },
            { "passes", string.Join(" ; ", top) },
            { "screen", Screen.width + "x" + Screen.height },
        });
    }

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
                case float fl: sb.Append(fl.ToString("0.####", CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n); break;
                default: sb.Append('"').Append((kv.Value ?? "").ToString().Replace("\\", "/").Replace("\"", "'")).Append('"'); break;
            }
        }
        sb.Append("}\n");
        File.AppendAllText(outPath, sb.ToString());
    }
}
