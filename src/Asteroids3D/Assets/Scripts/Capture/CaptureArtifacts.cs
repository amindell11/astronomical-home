using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Capture
{
    public sealed class CaptureArtifacts
    {
        private static readonly Regex SafeName = new("^[A-Za-z0-9_-]+$");
        private static readonly Regex PhaseColumn = new("^[A-Za-z_][A-Za-z0-9_]*$");

        private readonly Manifest manifest;
        private readonly List<string> phaseColumns = new();
        private readonly List<float> unscaledSeconds = new();
        private readonly List<float> simSeconds = new();
        private readonly List<Dictionary<string, float>> phaseRows = new();
        private Dictionary<string, float> pendingPhase;

        public string FrameDir { get; }

        public static bool IsSafeName(string name) => !string.IsNullOrEmpty(name) && SafeName.IsMatch(name);

        public CaptureArtifacts(CaptureConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Validate(config);

            var stamp = string.IsNullOrEmpty(config.runStamp)
                ? DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                : config.runStamp;
            var outputRoot = Path.IsPathRooted(config.outputRoot)
                ? config.outputRoot
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", config.outputRoot));
            FrameDir = Path.Combine(outputRoot, "frames", $"{stamp}-{config.clipName}");
            if (Directory.Exists(FrameDir) && Directory.GetFiles(FrameDir, "f_*.png").Length > 0)
                throw new InvalidOperationException(
                    $"[Capture] frame dir already contains frames (pick a distinct clipName/runStamp): {FrameDir}");
            Directory.CreateDirectory(FrameDir);
            manifest = new Manifest
            {
                width = config.width,
                height = config.height,
                everyFixedSteps = config.everyFixedSteps,
                fixedDeltaTime = Time.fixedDeltaTime,
                suggestedFps = 1f / (Time.fixedDeltaTime * config.everyFixedSteps),
            };
            WriteManifest();
            WriteProvenance(config);
            WritePhases();
        }

        public void RecordPhase(string column, float value)
        {
            if (string.IsNullOrEmpty(column) || !PhaseColumn.IsMatch(column))
                throw new ArgumentException($"[Capture] phase column must match [A-Za-z_][A-Za-z0-9_]* , got '{column}'.");
            if (!phaseColumns.Contains(column)) phaseColumns.Add(column);
            pendingPhase ??= new Dictionary<string, float>();
            pendingPhase[column] = value;
        }

        public void CommitFrame()
        {
            unscaledSeconds.Add(Time.unscaledTime);
            simSeconds.Add(Time.time);
            phaseRows.Add(pendingPhase ?? new Dictionary<string, float>());
            pendingPhase = null;
            WritePhases();
        }

        public void Complete(CaptureCost cost)
        {
            if (cost == null) throw new ArgumentNullException(nameof(cost));
            manifest.steps = cost.Steps;
            manifest.meanStepMs = cost.MeanMs();
            manifest.medianStepMs = cost.MedianMs();
            WriteManifest();
            WritePhases();
        }

        private static void Validate(CaptureConfig config)
        {
            if (config.width <= 0 || config.height <= 0 || config.width % 2 != 0 || config.height % 2 != 0)
                throw new ArgumentException(
                    $"[Capture] dimensions must be positive and even (yuv420p), got {config.width}x{config.height}");
            if (config.everyFixedSteps <= 0)
                throw new ArgumentException($"[Capture] everyFixedSteps must be > 0, got {config.everyFixedSteps}");
            if (config.minHalfHeight < 0f || config.padding < 0f)
                throw new ArgumentException(
                    $"[Capture] framing values must be nonnegative, got minHalfHeight={config.minHalfHeight} padding={config.padding}");
            if (string.IsNullOrEmpty(config.outputRoot))
                throw new ArgumentException("[Capture] outputRoot must be set");
            if (!IsSafeName(config.clipName))
                throw new ArgumentException(
                    $"[Capture] clipName must be filesystem-safe [A-Za-z0-9_-], got '{config.clipName}'");
            if (!string.IsNullOrEmpty(config.runStamp) && !IsSafeName(config.runStamp))
                throw new ArgumentException(
                    $"[Capture] runStamp must be filesystem-safe [A-Za-z0-9_-], got '{config.runStamp}'");
        }

        private void WriteManifest() =>
            File.WriteAllText(Path.Combine(FrameDir, "manifest.json"), JsonUtility.ToJson(manifest, true));

        private void WriteProvenance(CaptureConfig config)
        {
            var names = QualitySettings.names;
            var level = QualitySettings.GetQualityLevel();
            var provenance = new Provenance
            {
                source = string.IsNullOrEmpty(config.preset) ? "capture" : config.preset,
                size = config.width + "x" + config.height,
                dataKind = "frames",
                timingBasis = "unscaled",
                qualityLevel = level >= 0 && level < names.Length ? names[level] : level.ToString(CultureInfo.InvariantCulture),
                commit = RequireCommit(config.commit),
            };
            File.WriteAllText(Path.Combine(FrameDir, "provenance.json"), JsonUtility.ToJson(provenance, true));
        }

        private void WritePhases()
        {
            var csv = new StringBuilder();
            csv.Append("frame,unscaled_seconds,sim_seconds");
            foreach (var column in phaseColumns) csv.Append(',').Append(column);
            csv.Append('\n');
            for (var i = 0; i < unscaledSeconds.Count; i++)
            {
                csv.Append(i.ToString(CultureInfo.InvariantCulture)).Append(',');
                csv.Append(unscaledSeconds[i].ToString("R", CultureInfo.InvariantCulture)).Append(',');
                csv.Append(simSeconds[i].ToString("R", CultureInfo.InvariantCulture));
                var row = phaseRows[i];
                foreach (var column in phaseColumns)
                {
                    csv.Append(',');
                    if (row.TryGetValue(column, out var value))
                        csv.Append(value.ToString("R", CultureInfo.InvariantCulture));
                }
                csv.Append('\n');
            }
            File.WriteAllText(Path.Combine(FrameDir, "phases.csv"), csv.ToString());
        }

        private static string RequireCommit(string commit)
        {
            if (string.IsNullOrEmpty(commit) || commit.Length != 40)
                throw new ArgumentException(
                    "[Capture] commit must be the 40-character sha the launcher passed in (-captureCommit).");
            return commit;
        }

        [Serializable]
        private sealed class Manifest
        {
            public int width;
            public int height;
            public int everyFixedSteps;
            public float fixedDeltaTime;
            public float suggestedFps;
            public int steps;
            public float meanStepMs;
            public float medianStepMs;
        }

        [Serializable]
        private sealed class Provenance
        {
            public string source;
            public string size;
            public string dataKind;
            public string timingBasis;
            public string qualityLevel;
            public string commit;
        }
    }
}
