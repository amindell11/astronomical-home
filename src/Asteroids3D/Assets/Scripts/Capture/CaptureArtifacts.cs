using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Game.Capture
{
    public sealed class CaptureArtifacts
    {
        private static readonly Regex SafeName = new("^[A-Za-z0-9_-]+$");

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
            WriteManifest(config);
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

        private void WriteManifest(CaptureConfig config)
        {
            var manifest = new Manifest
            {
                width = config.width,
                height = config.height,
                everyFixedSteps = config.everyFixedSteps,
                fixedDeltaTime = Time.fixedDeltaTime,
                suggestedFps = 1f / (Time.fixedDeltaTime * config.everyFixedSteps),
            };
            File.WriteAllText(Path.Combine(FrameDir, "manifest.json"), JsonUtility.ToJson(manifest, true));
        }

        [Serializable]
        private sealed class Manifest
        {
            public int width;
            public int height;
            public int everyFixedSteps;
            public float fixedDeltaTime;
            public float suggestedFps;
        }
    }
}
