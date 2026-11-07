using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Capture.Recording
{
    public sealed class CaptureRequest
    {
        public string OutputRoot { get; }
        public string ClipName { get; }
        public string Commit { get; }
        public string RunStamp { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyDictionary<string, Func<double>> Measurements { get; }

        public CaptureRequest(string outputRoot, string clipName, string commit, int width = 960, int height = 540,
            string runStamp = null, IReadOnlyDictionary<string, Func<double>> measurements = null)
        {
            if (string.IsNullOrWhiteSpace(outputRoot)) throw new ArgumentException("Capture output root is required.");
            if (!CaptureArtifacts.IsSafeName(clipName)) throw new ArgumentException("Capture clip name must match [A-Za-z0-9_-]+.");
            if (runStamp != null && !CaptureArtifacts.IsSafeName(runStamp))
                throw new ArgumentException("Capture run stamp must match [A-Za-z0-9_-]+.");
            if (commit == null || !Regex.IsMatch(commit, "^[0-9a-fA-F]{40}$"))
                throw new ArgumentException("Capture commit must be the launcher's 40-character hexadecimal SHA.");
            if (width <= 0 || height <= 0 || width % 2 != 0 || height % 2 != 0)
                throw new ArgumentException("Capture dimensions must be positive and even for yuv420p encoding.");
            var readers = new SortedDictionary<string, Func<double>>(StringComparer.Ordinal);
            if (measurements != null)
                foreach (var reader in measurements)
                {
                    if (!Regex.IsMatch(reader.Key, "^[A-Za-z_][A-Za-z0-9_]*$") ||
                        reader.Key is "frame_id" or "filename" or "unscaled_seconds" or "sim_seconds")
                        throw new ArgumentException($"Invalid capture measurement name '{reader.Key}'.");
                    readers.Add(reader.Key, reader.Value ?? throw new ArgumentException($"Capture measurement '{reader.Key}' has no reader."));
                }
            OutputRoot = outputRoot;
            ClipName = clipName;
            Commit = commit;
            RunStamp = runStamp;
            Width = width;
            Height = height;
            Measurements = new ReadOnlyDictionary<string, Func<double>>(readers);
        }
    }
}
