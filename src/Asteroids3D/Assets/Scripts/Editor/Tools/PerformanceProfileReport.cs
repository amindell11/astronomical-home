using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditorInternal;
using UnityEngine;

namespace Tools.Editor
{
    public static class PerformanceProfileReport
    {
        public static void Generate()
        {
            var rawPath = RequiredEnvironmentPath("ASTRO_PROFILE_RAW");
            var outputPath = RequiredEnvironmentPath("ASTRO_PROFILE_SUMMARY");
            var historyLength = int.TryParse(Environment.GetEnvironmentVariable("ASTRO_PROFILE_FRAME_HISTORY"),
                out var requestedHistory) ? Math.Max(300, requestedHistory) : 2000;
            typeof(ProfilerDriver).GetMethod("SetMaxFrameHistoryLength", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, new object[] { historyLength });
            if (!ProfilerDriver.LoadProfile(rawPath, false))
                throw new InvalidOperationException($"Unity could not load profiler capture {rawPath}.");

            var frames = ReadFrames();
            if (frames.Count == 0)
                throw new InvalidOperationException($"Profiler capture {rawPath} contains no frames.");

            var frameTimes = frames.Select(frame => frame.frameTimeMs).ToArray();
            var gpuTimes = frames.Where(frame => frame.gpuTimeMs >= 0f).Select(frame => frame.gpuTimeMs).ToArray();
            var spikeFloor = Percentile(frameTimes, 0.95f);
            var spikeIndexes = frames.Where(frame => frame.frameTimeMs >= spikeFloor)
                .Select(frame => frame.index)
                .ToHashSet();

            var summary = new ProfileSummary
            {
                source = rawPath,
                frameCount = frames.Count,
                frameTimeMs = Distribution(frameTimes),
                gpuTimeMs = gpuTimes.Length > 0 ? Distribution(gpuTimes) : null,
                worstFrames = frames.OrderByDescending(frame => frame.frameTimeMs).Take(20).ToArray(),
                spikeMarkers = ReadSpikeMarkers(spikeIndexes).OrderByDescending(marker => marker.totalMs).Take(40).ToArray()
            };

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
            File.WriteAllText(outputPath, JsonUtility.ToJson(summary, true));
            Debug.Log($"[PerformanceProfile] wrote {summary.frameCount} frames to {outputPath}");
        }

        private static List<ProfileFrame> ReadFrames()
        {
            var frames = new List<ProfileFrame>();
            using var iterator = new ProfilerFrameDataIterator();
            for (var frame = ProfilerDriver.firstFrameIndex; frame >= 0; frame = ProfilerDriver.GetNextFrameIndex(frame))
            {
                iterator.SetRoot(frame, 0);
                frames.Add(new ProfileFrame
                {
                    index = frame,
                    frameTimeMs = iterator.frameTimeMS,
                    gpuTimeMs = iterator.frameGpuTimeMS
                });
                if (frame == ProfilerDriver.lastFrameIndex)
                    break;
            }
            return frames;
        }

        private static List<MarkerTotal> ReadSpikeMarkers(HashSet<int> spikeIndexes)
        {
            var totals = new Dictionary<string, MarkerTotal>(StringComparer.Ordinal);
            using var iterator = new ProfilerFrameDataIterator();
            foreach (var frame in spikeIndexes)
            {
                var threadCount = iterator.GetThreadCount(frame);
                for (var thread = 0; thread < threadCount; thread++)
                {
                    iterator.SetRoot(frame, thread);
                    var threadName = iterator.GetThreadName();
                    while (iterator.Next(true))
                    {
                        if (iterator.depth <= 0 || iterator.durationMS <= 0f || string.IsNullOrWhiteSpace(iterator.name))
                            continue;
                        if (threadName.StartsWith("Worker ", StringComparison.Ordinal) && iterator.name == "Idle")
                            continue;
                        if (threadName == "Dispatcher" && iterator.name == "Semaphore.WaitForSignal")
                            continue;

                        var key = $"{threadName}/{iterator.name}";
                        if (!totals.TryGetValue(key, out var total))
                        {
                            total = new MarkerTotal { thread = threadName, marker = iterator.name };
                            totals.Add(key, total);
                        }
                        total.totalMs += iterator.durationMS;
                        total.calls++;
                    }
                }
            }
            return totals.Values.ToList();
        }

        private static DistributionStats Distribution(float[] values)
        {
            Array.Sort(values);
            return new DistributionStats
            {
                min = values[0],
                median = Percentile(values, 0.5f),
                p95 = Percentile(values, 0.95f),
                p99 = Percentile(values, 0.99f),
                max = values[^1],
                mean = values.Average()
            };
        }

        private static float Percentile(float[] values, float percentile)
        {
            var sorted = values.OrderBy(value => value).ToArray();
            var position = Mathf.Clamp01(percentile) * (sorted.Length - 1);
            var lower = Mathf.FloorToInt(position);
            var upper = Mathf.CeilToInt(position);
            return Mathf.Lerp(sorted[lower], sorted[upper], position - lower);
        }

        private static string RequiredEnvironmentPath(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{name} must be set.");
            return Path.GetFullPath(value);
        }

        [Serializable]
        private sealed class ProfileSummary
        {
            public string source;
            public int frameCount;
            public DistributionStats frameTimeMs;
            public DistributionStats gpuTimeMs;
            public ProfileFrame[] worstFrames;
            public MarkerTotal[] spikeMarkers;
        }

        [Serializable]
        private sealed class DistributionStats
        {
            public float min;
            public float median;
            public float p95;
            public float p99;
            public float max;
            public float mean;
        }

        [Serializable]
        private sealed class ProfileFrame
        {
            public int index;
            public float frameTimeMs;
            public float gpuTimeMs;
        }

        [Serializable]
        private sealed class MarkerTotal
        {
            public string thread;
            public string marker;
            public float totalMs;
            public int calls;
        }
    }
}
