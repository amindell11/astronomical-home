using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Capture.Recording
{
    public sealed class CaptureArtifacts : IDisposable
    {
        private readonly Manifest manifest;
        private readonly string outputDirectory;
        private readonly string metadataPath;
        private readonly string provenancePath;
        private readonly StreamWriter metadata;
        public string FrameDirectory { get; }
        public int FrameCount => manifest.frameCount;

        public static bool IsSafeName(string name) => !string.IsNullOrEmpty(name) && Regex.IsMatch(name, "^[A-Za-z0-9_-]+$");

        public CaptureArtifacts(CaptureRequest request)
        {
            outputDirectory = Path.IsPathRooted(request.OutputRoot) ? request.OutputRoot
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", request.OutputRoot));
            var stamp = request.RunStamp ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            FrameDirectory = Path.Combine(outputDirectory, "frames", $"{stamp}-{request.ClipName}");
            if (Directory.Exists(FrameDirectory))
                throw new IOException($"Capture output already exists: {FrameDirectory}");
            Directory.CreateDirectory(FrameDirectory);
            manifest = new Manifest { width = request.Width, height = request.Height };
            WriteManifest();
            provenancePath = Path.Combine(FrameDirectory, "provenance.json");
            File.WriteAllText(provenancePath, JsonUtility.ToJson(new Provenance
            {
                commit = request.Commit,
                width = request.Width,
                height = request.Height,
                qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
            }, true));
            metadataPath = Path.Combine(FrameDirectory, "frames.csv");
            metadata = new StreamWriter(metadataPath, false, new UTF8Encoding(false)) { AutoFlush = true };
            metadata.Write("frame_id,filename,unscaled_seconds,sim_seconds");
            foreach (var name in request.Measurements.Keys) metadata.Write("," + name);
            metadata.WriteLine();
        }

        public string FramePath(int frameId) => Path.Combine(FrameDirectory, $"f_{frameId:00000}.png");

        public void AppendFrame(int frameId, double unscaled, double sim, IReadOnlyList<double> measurements)
        {
            var row = new StringBuilder();
            row.Append(frameId).Append(',').Append(Path.GetFileName(FramePath(frameId))).Append(',');
            row.Append(unscaled.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            row.Append(sim.ToString("R", CultureInfo.InvariantCulture));
            foreach (var value in measurements) row.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
            metadata.WriteLine(row);
            manifest.frameCount++;
        }

        public CaptureResult Complete()
        {
            metadata.Dispose();
            manifest.completed = true;
            WriteManifest();
            return new CaptureResult(outputDirectory, FrameDirectory, metadataPath, provenancePath, FrameCount);
        }

        public void Dispose() => metadata.Dispose();

        private void WriteManifest() => File.WriteAllText(Path.Combine(FrameDirectory, "manifest.json"), JsonUtility.ToJson(manifest, true));

        [Serializable]
        private sealed class Manifest
        {
            public int width;
            public int height;
            public int frameCount;
            public bool completed;
            public string frameMetadata = "frames.csv";
            public string timingBasis = "unscaled";
            public string finalFrameHold = "repeat-last-interval; single-frame=0.1s";
        }

        [Serializable]
        private sealed class Provenance
        {
            public string source = "native-game-view";
            public string dataKind = "png";
            public string timingBasis = "unscaled";
            public int width;
            public int height;
            public string qualityLevel;
            public string commit;
        }
    }
}