// SCRATCH diagnosis probe (flight fps, sibling of HangarPerfProbe). Never commit.
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
using UnityEngine.SceneManagement;

public sealed class FlightPerfProbe : MonoBehaviour
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FrameTiming[] Timing = new FrameTiming[1];

    private string outPath;
    private string forceLocale;
    private float settleSeconds = 8f;
    private float frozenTimeScale = 0.001f;
    private readonly List<Vector2Int> resolutions = new();
    private readonly List<int> qualities = new();
    private int warmFrames = 40;
    private int sampleFrames = 150;
    private bool shotTaken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        var path = Arg(args, "-flightPerfProbe", null);
        if (path == null) return;
        var go = new GameObject("FlightPerfProbe");
        DontDestroyOnLoad(go);
        var p = go.AddComponent<FlightPerfProbe>();
        p.outPath = path;
        p.forceLocale = Arg(args, "-probeLocale", null);
        p.settleSeconds = float.Parse(Arg(args, "-probeSettle", "8"), CultureInfo.InvariantCulture);
        p.frozenTimeScale = float.Parse(Arg(args, "-probeTimeScale", "0.001"), CultureInfo.InvariantCulture);
        p.sampleFrames = int.Parse(Arg(args, "-probeFrames", "150"));
        foreach (var r in Arg(args, "-probeRes", "1920x1080,3840x2160").Split(','))
        {
            var wh = r.Split('x');
            p.resolutions.Add(new Vector2Int(int.Parse(wh[0]), int.Parse(wh[1])));
        }
        foreach (var q in Arg(args, "-probeQuality", "0,2").Split(','))
            p.qualities.Add(int.Parse(q));
    }

    private static string Arg(string[] args, string key, string fallback)
    {
        var i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private sealed class Ctx
    {
        public List<Renderer> sky = new();
        public Camera observer;
        public List<Camera> otherCams = new();
        public List<Canvas> canvases = new();
        public List<(Component data, PropertyInfo prop, bool original)> post = new();
        public object urp;
        public PropertyInfo renderScale;
        public PropertyInfo msaa;
    }

    private IEnumerator Start()
    {
        var deadline = Time.realtimeSinceStartup + 180f;
        while (!Find("HangarScreen") && Time.realtimeSinceStartup < deadline) yield return null;
        var hangar = Find("HangarScreen");
        if (!hangar)
        {
            Fail("hangar never appeared");
            yield break;
        }
        yield return new WaitForSecondsRealtime(3f);

        var host = Find("GameHost");
        var config = Get(Get(Get(host, "sessionProfile"), "sectorEntry"), "config") as UnityEngine.Object;
        var sceneRef = Get(config, "locale");
        var configuredLocale = (string)Get(sceneRef, "sceneName");
        if (!string.IsNullOrEmpty(forceLocale))
            sceneRef.GetType().GetField("sceneName", Any).SetValue(sceneRef, forceLocale);

        Write(new Dictionary<string, object>
        {
            { "header", true },
            { "mode", "flight" },
            { "editor", Application.isEditor },
            { "device", SystemInfo.graphicsDeviceName },
            { "api", SystemInfo.graphicsDeviceType.ToString() },
            { "ftmEnabled", FrameTimingManager.IsFeatureEnabled() },
            { "gpuRecorder", SystemInfo.supportsGpuRecorder },
            { "hdrActive", HDROutputSettings.main.active },
            { "refreshHz", Screen.currentResolution.refreshRateRatio.value },
            { "desktop", Screen.currentResolution.width + "x" + Screen.currentResolution.height },
            { "sectorConfig", config ? config.name : "null" },
            { "configuredLocale", configuredLocale },
            { "forcedLocale", forceLocale ?? "" },
            { "frozenTimeScale", frozenTimeScale },
        });

        var button = Get(hangar, "launchButton");
        var onClick = button.GetType().GetProperty("onClick").GetValue(button);
        onClick.GetType().GetMethod("Invoke", Type.EmptyTypes).Invoke(onClick, null);

        deadline = Time.realtimeSinceStartup + 180f;
        while (!InFlight(host) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!InFlight(host))
        {
            Fail("flight never started; active scene " + SceneManager.GetActiveScene().name);
            yield break;
        }

        // ResetDamageState on the sector's entry respawn clears invulnerability, so grant it after load.
        var damage = Get(Get(host, "rigInstance"), "Player") is Component player ? Get(player, "Damage") : null;
        damage?.GetType().GetMethod("SetInvulnerability").Invoke(damage, new object[] { 36000f });
        Write(new Dictionary<string, object>
        {
            { "flight", true },
            { "activeScene", SceneManager.GetActiveScene().name },
            { "sector", Get(host, "ActiveSector") is UnityEngine.Object s && s ? s.name : "null" },
            { "invulnerable", damage != null },
            { "loadSeconds", Time.realtimeSinceStartup },
        });

        yield return new WaitForSecondsRealtime(settleSeconds);
        Time.timeScale = frozenTimeScale;
        yield return new WaitForSecondsRealtime(1f);

        var originalQuality = QualitySettings.GetQualityLevel();
        foreach (var res in resolutions)
        {
            yield return SetResolution(res);
            foreach (var q in qualities)
            {
                yield return SetQuality(q);
                yield return RunMatrix();
            }
        }

        // Live arms: the sim runs, so frames differ arm to arm; a sanity check on the frozen numbers.
        Time.timeScale = 1f;
        foreach (var res in resolutions)
        {
            yield return SetResolution(res);
            foreach (var q in qualities)
            {
                yield return SetQuality(q);
                var ctx = Snapshot();
                QualitySettings.vSyncCount = 0;
                yield return Measure("live_base", ctx);
                SetAll(ctx.sky, false);
                yield return Measure("live_no_sky", ctx);
                SetAll(ctx.sky, true);
                yield return Measure("live_base_again", ctx);
                Write(new Dictionary<string, object> { { "liveScene", Describe() } });
            }
        }

        if (QualitySettings.GetQualityLevel() != originalQuality)
            QualitySettings.SetQualityLevel(originalQuality, true);
        Write(new Dictionary<string, object> { { "done", true }, { "seconds", Time.realtimeSinceStartup } });
        Application.Quit();
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
        if (splash && splash.GetComponent<Canvas>().enabled) return false;
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

    private static string Describe()
    {
        var visible = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.isVisible).ToList();
        var byLayer = visible.GroupBy(r => LayerMask.LayerToName(r.gameObject.layer))
            .OrderByDescending(g => g.Count()).Select(g => g.Key + ":" + g.Count());
        return "visibleRenderers=" + visible.Count + " [" + string.Join(" ", byLayer) + "] particles=" +
               FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(ps => ps.isPlaying);
    }

    private static Ctx Snapshot()
    {
        var ctx = new Ctx();
        var skyLayer = LayerMask.NameToLayer("Sky");
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.enabled && r.gameObject.activeInHierarchy && r.gameObject.layer == skyLayer)
                ctx.sky.Add(r);
        var observer = Find("ObserverCam");
        ctx.observer = observer ? observer.GetComponent<Camera>() : null;
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.enabled && c != ctx.observer)
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
            { "locale", SceneManager.GetActiveScene().name },
            { "sky", string.Join(" | ", ctx.sky.Select(Name)) },
            { "cameras", string.Join(" | ", FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.enabled)
                .Select(c => c.name + (c.targetTexture ? "->RT" : "") + " mask=" + c.cullingMask +
                             (c.orthographic ? " ortho=" + c.orthographicSize.ToString("0.##", CultureInfo.InvariantCulture) : " fov=" + c.fieldOfView))) },
            { "canvases", string.Join(" | ", ctx.canvases.Select(c => c.name + ":" + c.renderMode)) },
            { "scene", Describe() },
            { "timeScale", Time.timeScale },
        });

        if (!shotTaken)
        {
            shotTaken = true;
            ScreenCapture.CaptureScreenshot(Path.ChangeExtension(outPath, ".png"));
            for (var i = 0; i < 5; i++) yield return null;
        }

        yield return Measure("base_tierVsync", ctx);
        QualitySettings.vSyncCount = 0;
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

        foreach (var r in ctx.sky)
        {
            SetAll(ctx.sky, false);
            r.enabled = true;
            yield return Measure("only_sky:" + Name(r), ctx);
        }
        SetAll(ctx.sky, true);

        foreach (var c in ctx.otherCams)
        {
            c.enabled = false;
            yield return Measure("no_cam:" + c.name, ctx);
            c.enabled = true;
        }

        SetCanvases(ctx.canvases, false);
        yield return Measure("no_any_ui", ctx);
        SetCanvases(ctx.canvases, true);

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
        SetCanvases(ctx.canvases, false);
        SetPost(ctx, false);
        yield return Measure("floor_sky_ui_post_off", ctx);
        SetAll(ctx.sky, true);
        yield return Measure("sky_on_no_ui_no_post", ctx);
        SetPost(ctx, true);
        SetCanvases(ctx.canvases, true);
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

        Write(new Dictionary<string, object>
        {
            { "label", label },
            { "quality", QualitySettings.names[QualitySettings.GetQualityLevel()] },
            { "screen", Screen.width + "x" + Screen.height },
            { "locale", SceneManager.GetActiveScene().name },
            { "timeScale", Time.timeScale },
            { "vsync", QualitySettings.vSyncCount },
            { "renderScale", ctx.renderScale?.GetValue(ctx.urp) ?? -1f },
            { "msaa", ctx.msaa?.GetValue(ctx.urp) ?? -1 },
            { "previewRT", "none" },
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

    private IEnumerator Passes()
    {
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
                case float fl: sb.Append(fl.ToString("0.####", CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n); break;
                default: sb.Append('"').Append((kv.Value ?? "").ToString().Replace("\\", "/").Replace("\"", "'")).Append('"'); break;
            }
        }
        sb.Append("}\n");
        File.AppendAllText(outPath, sb.ToString());
    }
}
