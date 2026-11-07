// SCRATCH perf-pass build + .raw analysis hooks. Never commit.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

public static class PerfPassProbeEditor
{
    private static string Arg(string key)
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    public static void Build()
    {
        var outDir = Arg("-probeOut");
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

    private static string Ms(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    public static void Analyze()
    {
        var raw = Arg("-probeRaw");
        var outPath = Arg("-probeReport");
        var sb = new StringBuilder();
        try
        {
            if (!ProfilerDriver.LoadProfile(raw, false)) throw new Exception("LoadProfile failed: " + raw);
            var first = ProfilerDriver.firstFrameIndex;
            var last = ProfilerDriver.lastFrameIndex;
            sb.AppendLine("raw=" + raw + " frames=" + first + ".." + last);

            var frameMs = new Dictionary<int, double>();
            var pathTotals = new Dictionary<string, List<double>>();
            var selfAll = new Dictionary<string, double>();
            var gcAll = new Dictionary<string, double>();
            var callsAll = new Dictionary<string, double>();
            var frames = Enumerable.Range(first, last - first + 1).ToList();
            var children = new List<int>();

            foreach (var f in frames)
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
                if (view == null || !view.valid) continue;
                frameMs[f] = view.frameTimeMs;
                Walk(view, view.GetRootItemID(), "", 0, 7, children, (path, name, total, self, gc, calls) =>
                {
                    if (!pathTotals.TryGetValue(path, out var l)) pathTotals[path] = l = new List<double>();
                    l.Add(total);
                    selfAll[name] = selfAll.GetValueOrDefault(name) + self;
                    gcAll[name] = gcAll.GetValueOrDefault(name) + gc;
                    callsAll[name] = callsAll.GetValueOrDefault(name) + calls;
                });
            }

            var n = frameMs.Count;
            var sorted = frameMs.Values.OrderBy(x => x).ToList();
            double P(double p) => sorted[Math.Min(n - 1, (int)(p * (n - 1) + 0.5))];
            sb.AppendLine("mainThreadFrameMs n=" + n + " med=" + Ms(P(0.5)) + " p95=" + Ms(P(0.95)) + " p99=" + Ms(P(0.99)) + " max=" + Ms(sorted[n - 1]));
            sb.AppendLine("frames>16.7=" + sorted.Count(x => x > 16.67) + " >25=" + sorted.Count(x => x > 25) + " >33=" + sorted.Count(x => x > 33.3));

            sb.AppendLine();
            sb.AppendLine("== Top self time per frame (mean over all frames), with GC KB/frame and calls/frame");
            foreach (var kv in selfAll.OrderByDescending(k => k.Value).Take(45))
                sb.AppendLine("  " + Ms(kv.Value / n).PadLeft(7) + " ms  gc " + (gcAll[kv.Key] / n / 1024).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(7) +
                              "KB  calls " + (callsAll[kv.Key] / n).ToString("0.#", CultureInfo.InvariantCulture).PadLeft(6) + "  " + kv.Key);

            sb.AppendLine();
            sb.AppendLine("== Top GC allocators (KB/frame mean)");
            foreach (var kv in gcAll.OrderByDescending(k => k.Value).Take(20))
                sb.AppendLine("  " + (kv.Value / n / 1024).ToString("0.00", CultureInfo.InvariantCulture).PadLeft(8) + " KB  " + kv.Key);

            sb.AppendLine();
            sb.AppendLine("== Inclusive path time: mean / p99 / max (ms), paths with mean >= 0.10 or max >= 4");
            foreach (var kv in pathTotals
                         .Select(k => (k.Key, mean: k.Value.Sum() / n, p99: Pct(k.Value, n, 0.99), max: k.Value.Max()))
                         .Where(k => k.mean >= 0.10 || k.max >= 4)
                         .OrderBy(k => k.Key))
                sb.AppendLine("  " + Ms(kv.mean).PadLeft(7) + " " + Ms(kv.p99).PadLeft(7) + " " + Ms(kv.max).PadLeft(7) + "  " + kv.Key);

            var spikes = frameMs.OrderByDescending(k => k.Value).Take(12).ToList();
            foreach (var (f, ms) in spikes)
            {
                sb.AppendLine();
                sb.AppendLine("== Spike frame " + f + " main thread " + Ms(ms) + " ms (items >= 0.5 ms)");
                using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
                Dump(view, view.GetRootItemID(), 0, 12, 0.5f, sb, new List<int>());
            }

            sb.AppendLine();
            sb.AppendLine("== Threads in frame " + first + 10);
            for (var t = 0; t < 64; t++)
            {
                using var rv = ProfilerDriver.GetRawFrameDataView(first + 10, t);
                if (rv == null || !rv.valid) break;
                sb.AppendLine("  " + t + " " + rv.threadGroupName + " / " + rv.threadName);
            }
            sb.AppendLine();
            sb.AppendLine("== Render thread top self time per frame");
            var rtSelf = new Dictionary<string, double>();
            var rtCount = 0;
            foreach (var f in frames)
            {
                var ti = -1;
                for (var t = 0; t < 64; t++)
                {
                    using var rv = ProfilerDriver.GetRawFrameDataView(f, t);
                    if (rv == null || !rv.valid) break;
                    if (rv.threadName == "Render Thread") { ti = t; break; }
                }
                if (ti < 0) continue;
                using var view = ProfilerDriver.GetHierarchyFrameDataView(f, ti,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
                if (view == null || !view.valid) continue;
                rtCount++;
                Walk(view, view.GetRootItemID(), "", 0, 12, new List<int>(), (path, name, total, self, gc, calls) =>
                    rtSelf[name] = rtSelf.GetValueOrDefault(name) + self);
            }
            foreach (var kv in rtSelf.OrderByDescending(k => k.Value).Take(20))
                sb.AppendLine("  " + Ms(kv.Value / Math.Max(1, rtCount)).PadLeft(7) + " ms  " + kv.Key);
        }
        catch (Exception e)
        {
            sb.AppendLine("ERROR " + e);
        }
        File.WriteAllText(outPath, sb.ToString());
        EditorApplication.Exit(0);
    }

    private static double Pct(List<double> xs, int n, double p)
    {
        var s = xs.Concat(Enumerable.Repeat(0.0, Math.Max(0, n - xs.Count))).OrderBy(x => x).ToList();
        return s[Math.Min(s.Count - 1, (int)(p * (s.Count - 1) + 0.5))];
    }

    private static void Walk(HierarchyFrameDataView view, int id, string path, int depth, int maxDepth, List<int> scratch,
        Action<string, string, double, double, double, double> visit)
    {
        var kids = new List<int>();
        view.GetItemChildren(id, kids);
        foreach (var k in kids)
        {
            var name = view.GetItemName(k);
            var p = path.Length == 0 ? name : path + " > " + name;
            visit(p, name,
                view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnTotalTime),
                view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnSelfTime),
                view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnGcMemory),
                view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnCalls));
            if (depth + 1 < maxDepth) Walk(view, k, p, depth + 1, maxDepth, scratch, visit);
        }
    }

    private static void Dump(HierarchyFrameDataView view, int id, int depth, int maxDepth, float minMs, StringBuilder sb, List<int> scratch)
    {
        var kids = new List<int>();
        view.GetItemChildren(id, kids);
        foreach (var k in kids)
        {
            var total = view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnTotalTime);
            if (total < minMs) continue;
            sb.AppendLine(new string(' ', 2 + depth * 2) + Ms(total) + " (self " + Ms(view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnSelfTime)) +
                          ", calls " + view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnCalls) + ", gc " +
                          (view.GetItemColumnDataAsFloat(k, HierarchyFrameDataView.columnGcMemory) / 1024).ToString("0.0", CultureInfo.InvariantCulture) + "KB) " +
                          view.GetItemName(k));
            if (depth + 1 < maxDepth) Dump(view, k, depth + 1, maxDepth, minMs, sb, scratch);
        }
    }
}
