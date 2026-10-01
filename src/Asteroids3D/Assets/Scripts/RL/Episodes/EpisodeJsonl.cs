using System;
using System.Globalization;
using System.IO;
using Substrate.Results;

namespace RL.Episodes
{
    /// <summary>Timestamped result sink under repo-root results/, shared by the tests and the RL hosts.</summary>
    public static class EpisodeJsonl
    {
        public static string NewRunPath(string tag, string folder = "rl-episodes",
            string dirOverride = null, string workerSuffix = null)
        {
            // dirOverride is the launcher-owned absolute results dir (run_parallel.py --harness-jsonl-dir):
            // the exact location the parallel gate reads back, so parallel workers don't each reconstruct it.
            var dir = string.IsNullOrEmpty(dirOverride) ? ResultsRoot.Folder(folder) : dirOverride;
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(dir, $"{stamp}-{tag}{workerSuffix}.jsonl");
        }

        public static void Append(string path, in EpisodeResult result) =>
            File.AppendAllText(path, result.ToJsonLine() + "\n");
    }
}
