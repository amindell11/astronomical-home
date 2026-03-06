using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Diagnostics.Performance
{
    public sealed class LatencyMetricsCollector : MonoBehaviour
    {
        private readonly List<FrameSample> frameSamples = new();
        private readonly List<string> notes = new();

        private LatencyProfilingSettings settings;
        private ProfilerRecorder mainThreadRecorder;
        private ProfilerRecorder renderThreadRecorder;
        private ProfilerRecorder gcAllocRecorder;
        private MarkerRecorder[] markerRecorders;
        private int gcCollectionsAtStart;
        private bool isSampling;
        private string startedAtUtc;

        public void Configure(LatencyProfilingSettings profilingSettings)
        {
            settings = profilingSettings;
            startedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            mainThreadRecorder = StartOptionalRecorder(ProfilerCategory.Internal, "Main Thread");
            renderThreadRecorder = StartOptionalRecorder(ProfilerCategory.Render, "Render Thread");
            gcAllocRecorder = StartOptionalRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");

            markerRecorders = new[]
            {
                new MarkerRecorder(LatencyProfilingMarkers.AIUpdateName, StartOptionalRecorder(ProfilerCategory.Scripts, LatencyProfilingMarkers.AIUpdateName)),
                new MarkerRecorder(LatencyProfilingMarkers.ProjectileFireName, StartOptionalRecorder(ProfilerCategory.Scripts, LatencyProfilingMarkers.ProjectileFireName)),
                new MarkerRecorder(LatencyProfilingMarkers.ProjectileUpdateName, StartOptionalRecorder(ProfilerCategory.Scripts, LatencyProfilingMarkers.ProjectileUpdateName)),
                new MarkerRecorder(LatencyProfilingMarkers.ObjectiveTrackerName, StartOptionalRecorder(ProfilerCategory.Scripts, LatencyProfilingMarkers.ObjectiveTrackerName)),
                new MarkerRecorder(LatencyProfilingMarkers.AsteroidSpawnName, StartOptionalRecorder(ProfilerCategory.Scripts, LatencyProfilingMarkers.AsteroidSpawnName)),
            };

            if (!mainThreadRecorder.Valid)
                notes.Add("Main thread recorder was unavailable on this runtime.");
            if (!renderThreadRecorder.Valid)
                notes.Add("Render thread recorder was unavailable on this runtime.");
            if (!gcAllocRecorder.Valid)
                notes.Add("GC allocation recorder was unavailable on this runtime.");

            foreach (var marker in markerRecorders.Where(marker => !marker.Recorder.Valid))
                notes.Add($"Profiler marker recorder was unavailable for '{marker.Name}'.");
        }

        public void BeginSampling()
        {
            frameSamples.Clear();
            gcCollectionsAtStart = GetGcCollectionCount();
            isSampling = true;
        }

        public LatencyProfileResult CompleteSampling()
        {
            isSampling = false;

            var summary = new LatencyProfileSummary
            {
                scenario = settings.Scenario,
                warmupSeconds = settings.WarmupSeconds,
                sampleSeconds = settings.SampleSeconds,
                seed = settings.Seed,
                startedAtUtc = startedAtUtc,
                completedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                productName = Application.productName,
                frameTimeMs = BuildFrameStats(frameSamples.Select(sample => sample.FrameTimeMs).ToList()),
                mainThreadMs = BuildSeriesStats(frameSamples.Where(sample => sample.MainThreadMs.HasValue).Select(sample => sample.MainThreadMs.Value).ToList()),
                renderThreadMs = BuildSeriesStats(frameSamples.Where(sample => sample.RenderThreadMs.HasValue).Select(sample => sample.RenderThreadMs.Value).ToList()),
                gc = BuildGcStats(frameSamples, GetGcCollectionCount() - gcCollectionsAtStart),
                markers = BuildMarkerStats(frameSamples),
                notes = notes.ToArray()
            };

            var csv = BuildCsv(frameSamples);
            DisposeRecorders();
            return new LatencyProfileResult(summary, csv);
        }

        private void Update()
        {
            if (!isSampling)
                return;

            var sample = new FrameSample
            {
                FrameIndex = frameSamples.Count,
                FrameTimeMs = Time.unscaledDeltaTime * 1000d,
                MainThreadMs = ReadMilliseconds(mainThreadRecorder),
                RenderThreadMs = ReadMilliseconds(renderThreadRecorder),
                GcAllocBytes = ReadBytes(gcAllocRecorder),
                MarkerValuesMs = ReadMarkerValues(markerRecorders)
            };

            frameSamples.Add(sample);
        }

        private static ProfilerRecorder StartOptionalRecorder(ProfilerCategory category, string statName)
        {
            try
            {
                return ProfilerRecorder.StartNew(category, statName);
            }
            catch
            {
                return default;
            }
        }

        private static double? ReadMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return null;

            return recorder.LastValue / 1_000_000d;
        }

        private static long? ReadBytes(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return null;

            return recorder.LastValue;
        }

        private static double?[] ReadMarkerValues(IReadOnlyList<MarkerRecorder> markers)
        {
            var values = new double?[markers.Count];
            for (var i = 0; i < markers.Count; i++)
            {
                values[i] = markers[i].Recorder.Valid
                    ? markers[i].Recorder.LastValue / 1_000_000d
                    : null;
            }

            return values;
        }

        private static LatencyFrameStats BuildFrameStats(List<double> values)
        {
            var stats = BuildSeriesStats(values);
            return new LatencyFrameStats
            {
                available = stats.available,
                sampleCount = stats.sampleCount,
                average = stats.average,
                p50 = stats.p50,
                p95 = stats.p95,
                p99 = stats.p99,
                max = stats.max,
                hitchOver16_7 = values.Count(value => value > 16.7d),
                hitchOver33_3 = values.Count(value => value > 33.3d),
                hitchOver50 = values.Count(value => value > 50d)
            };
        }

        private static LatencySeriesStats BuildSeriesStats(List<double> values)
        {
            if (values == null || values.Count == 0)
                return new LatencySeriesStats { available = false };

            values.Sort();
            return new LatencySeriesStats
            {
                available = true,
                sampleCount = values.Count,
                average = values.Average(),
                p50 = Percentile(values, 0.50d),
                p95 = Percentile(values, 0.95d),
                p99 = Percentile(values, 0.99d),
                max = values[^1]
            };
        }

        private static LatencyGcStats BuildGcStats(List<FrameSample> samples, int collectionsDelta)
        {
            var bytes = samples.Where(sample => sample.GcAllocBytes.HasValue).Select(sample => sample.GcAllocBytes.Value).ToList();
            var totalAlloc = bytes.Sum();
            var frameCount = Math.Max(1, samples.Count);

            return new LatencyGcStats
            {
                available = bytes.Count > 0,
                allocatedBytes = totalAlloc,
                allocatedBytesPerFrame = totalAlloc / (double)frameCount,
                collectionCount = collectionsDelta
            };
        }

        private LatencyMarkerStats[] BuildMarkerStats(List<FrameSample> samples)
        {
            var stats = new List<LatencyMarkerStats>(markerRecorders.Length);

            for (var i = 0; i < markerRecorders.Length; i++)
            {
                var values = samples
                    .Select(sample => sample.MarkerValuesMs.Length > i ? sample.MarkerValuesMs[i] : null)
                    .Where(value => value.HasValue)
                    .Select(value => value.Value)
                    .ToList();

                var seriesStats = BuildSeriesStats(values);
                stats.Add(new LatencyMarkerStats
                {
                    name = markerRecorders[i].Name,
                    available = seriesStats.available,
                    sampleCount = seriesStats.sampleCount,
                    average = seriesStats.average,
                    p95 = seriesStats.p95,
                    max = seriesStats.max
                });
            }

            return stats.ToArray();
        }

        private static string BuildCsv(IEnumerable<FrameSample> samples)
        {
            var builder = new StringBuilder();
            builder.AppendLine("frame,frame_time_ms,main_thread_ms,render_thread_ms,gc_alloc_bytes,ai_update_ms,projectile_fire_ms,projectile_update_ms,objective_tracker_ms,asteroid_spawn_ms");

            foreach (var sample in samples)
            {
                builder.Append(sample.FrameIndex);
                builder.Append(',');
                builder.Append(Format(sample.FrameTimeMs));
                builder.Append(',');
                builder.Append(Format(sample.MainThreadMs));
                builder.Append(',');
                builder.Append(Format(sample.RenderThreadMs));
                builder.Append(',');
                builder.Append(sample.GcAllocBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

                for (var i = 0; i < 5; i++)
                {
                    builder.Append(',');
                    builder.Append(Format(sample.MarkerValuesMs.Length > i ? sample.MarkerValuesMs[i] : null));
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string Format(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0)
                return 0d;

            var position = (sortedValues.Count - 1) * percentile;
            var lower = Mathf.FloorToInt((float)position);
            var upper = Mathf.CeilToInt((float)position);

            if (lower == upper)
                return sortedValues[lower];

            var weight = position - lower;
            return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * weight;
        }

        private static int GetGcCollectionCount()
        {
            return GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        }

        private void OnDestroy()
        {
            DisposeRecorders();
        }

        private void DisposeRecorders()
        {
            if (mainThreadRecorder.Valid)
                mainThreadRecorder.Dispose();
            if (renderThreadRecorder.Valid)
                renderThreadRecorder.Dispose();
            if (gcAllocRecorder.Valid)
                gcAllocRecorder.Dispose();

            if (markerRecorders == null)
                return;

            for (var i = 0; i < markerRecorders.Length; i++)
            {
                if (markerRecorders[i].Recorder.Valid)
                    markerRecorders[i].Recorder.Dispose();
            }
        }

        private readonly struct MarkerRecorder
        {
            public MarkerRecorder(string name, ProfilerRecorder recorder)
            {
                Name = name;
                Recorder = recorder;
            }

            public string Name { get; }
            public ProfilerRecorder Recorder { get; }
        }

        private sealed class FrameSample
        {
            public int FrameIndex;
            public double FrameTimeMs;
            public double? MainThreadMs;
            public double? RenderThreadMs;
            public long? GcAllocBytes;
            public double?[] MarkerValuesMs;
        }
    }

    public sealed class LatencyProfileResult
    {
        public LatencyProfileResult(LatencyProfileSummary summary, string frameTimesCsv)
        {
            Summary = summary;
            FrameTimesCsv = frameTimesCsv;
        }

        public LatencyProfileSummary Summary { get; }
        public string FrameTimesCsv { get; }

        public string ToSummaryJson()
        {
            return JsonUtility.ToJson(Summary, true);
        }
    }

    [Serializable]
    public sealed class LatencyProfileSummary
    {
        public string scenario;
        public int warmupSeconds;
        public int sampleSeconds;
        public int seed;
        public string startedAtUtc;
        public string completedAtUtc;
        public string unityVersion;
        public string productName;
        public LatencyFrameStats frameTimeMs;
        public LatencySeriesStats mainThreadMs;
        public LatencySeriesStats renderThreadMs;
        public LatencyGcStats gc;
        public LatencyMarkerStats[] markers;
        public string[] notes;
    }

    [Serializable]
    public class LatencySeriesStats
    {
        public bool available;
        public int sampleCount;
        public double average;
        public double p50;
        public double p95;
        public double p99;
        public double max;
    }

    [Serializable]
    public sealed class LatencyFrameStats : LatencySeriesStats
    {
        public int hitchOver16_7;
        public int hitchOver33_3;
        public int hitchOver50;
    }

    [Serializable]
    public sealed class LatencyGcStats
    {
        public bool available;
        public long allocatedBytes;
        public double allocatedBytesPerFrame;
        public int collectionCount;
    }

    [Serializable]
    public sealed class LatencyMarkerStats
    {
        public string name;
        public bool available;
        public int sampleCount;
        public double average;
        public double p95;
        public double max;
    }
}
