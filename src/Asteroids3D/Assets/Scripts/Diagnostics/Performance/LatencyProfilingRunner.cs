using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace Diagnostics.Performance
{
    /// <summary>
    /// Drives a latency profiling session: warmup, sample, write artifacts, quit.
    /// Created and owned by MainGameManager — not self-bootstrapping.
    /// </summary>
    public sealed class LatencyProfilingRunner
    {
        private readonly LatencyProfilingSettings settings;
        private readonly StringBuilder trace = new();
        private readonly string tracePath;
        private LatencyMetricsCollector collector;

        public LatencyProfilingRunner(LatencyProfilingSettings profilingSettings)
        {
            settings = profilingSettings ?? throw new ArgumentNullException(nameof(profilingSettings));

            Directory.CreateDirectory(settings.GetResolvedOutDir());
            tracePath = Path.Combine(settings.GetResolvedOutDir(), "latency-runner.log");

            AppendTrace("runner created");
            AppendTrace("scenario: " + settings.Scenario);
            ApplyRuntimeSettings();
            AppendTrace("runtime settings applied");
        }

        /// <summary>
        /// Call from MainGameManager once InSector is reached.
        /// Attach the collector to an existing GameObject (e.g. MainGameManager's).
        /// Returns a coroutine that runs warmup → sample → write → quit.
        /// </summary>
        public IEnumerator RunSession(GameObject host)
        {
            collector = host.AddComponent<LatencyMetricsCollector>();
            collector.Configure(settings);
            AppendTrace("collector configured on " + host.name);

            Debug.Log($"[LatencyProfiling] Warmup started for scenario '{settings.Scenario}'.");
            AppendTrace("warmup started");
            yield return new WaitForSecondsRealtime(settings.WarmupSeconds);

            collector.BeginSampling();
            AppendTrace("sampling started");
            Debug.Log($"[LatencyProfiling] Sampling {settings.SampleSeconds}s into '{settings.GetResolvedOutDir()}'.");
            yield return new WaitForSecondsRealtime(settings.SampleSeconds);

            var result = collector.CompleteSampling();
            WriteArtifacts(result);
            AppendTrace("artifacts written — exiting");
            FlushTrace();

            Debug.Log("[LatencyProfiling] Capture complete. Exiting player.");
            yield return null;
            Application.Quit();
        }

        /// <summary>
        /// Write an error file and quit with exit code 2.
        /// Call when startup times out before reaching InSector.
        /// </summary>
        public void FailAndQuit(string message)
        {
            Debug.LogError($"[LatencyProfiling] {message}");
            AppendTrace($"failure: {message}");
            var outDir = settings.GetResolvedOutDir();
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "error.txt"), message + Environment.NewLine);
            FlushTrace();
            Application.Quit(2);
        }

        private void ApplyRuntimeSettings()
        {
            if (settings.DisableVSync)
                QualitySettings.vSyncCount = 0;

            Application.targetFrameRate = -1;

            if (!string.IsNullOrWhiteSpace(settings.QualityLevel))
            {
                var qualityIndex = Array.IndexOf(QualitySettings.names, settings.QualityLevel);
                if (qualityIndex >= 0)
                    QualitySettings.SetQualityLevel(qualityIndex, true);
            }

            Screen.SetResolution(settings.Width, settings.Height, false);
        }

        private void WriteArtifacts(LatencyProfileResult result)
        {
            var outDir = settings.GetResolvedOutDir();
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "summary.json"), result.ToSummaryJson());
            File.WriteAllText(Path.Combine(outDir, "frame-times.csv"), result.FrameTimesCsv);
        }

        private void AppendTrace(string message)
        {
            trace.AppendLine($"{DateTimeOffset.UtcNow:O} {message}");
        }

        private void FlushTrace()
        {
            if (string.IsNullOrWhiteSpace(tracePath))
                return;
            File.WriteAllText(tracePath, trace.ToString());
        }
    }
}
