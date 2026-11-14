using System;
using System.Globalization;
using System.IO;
using Substrate.Results;

namespace RL.Episodes
{
    /// <summary>Timestamped result sink under repo-root results/, shared by the tests and the RL hosts.</summary>
    public static class EpisodeJsonl
    {
        public static string NewRunPath(string tag, string folder, string dirOverride)
        {
            // dirOverride is the launcher-owned absolute results dir (eval_lane.py's out dir):
            // the exact location the launcher reads back, so it never reconstructs it.
            var dir = string.IsNullOrEmpty(dirOverride) ? ResultsRoot.Folder(folder) : dirOverride;
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(dir, $"{stamp}-{tag}.jsonl");
        }

        public static void Append(string path, in EpisodeResult result) =>
            File.AppendAllText(path, result.ToJsonLine() + "\n");
    }
}
