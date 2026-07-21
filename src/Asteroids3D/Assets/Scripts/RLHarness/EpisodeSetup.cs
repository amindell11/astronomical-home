using System;
using System.Globalization;
using System.IO;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    [Serializable]
    public struct SpawnPoses
    {
        public Vector2 agentPos;
        public float agentRotDeg;
        public Vector2 baselinePos;
        public float baselineRotDeg;
    }

    /// <summary>Pure pose derivation from (runSeed, episodeIndex): separation band + bearing + facings, so any episode replays from its index.</summary>
    public static class EpisodePoses
    {
        public static SpawnPoses Derive(in RewardSpec spec, int episodeIndex, Vector2 arenaCenter)
        {
            var rng = new System.Random(new SeedScope(spec.runSeed).Derive((uint)episodeIndex).ToSeed());
            var separation = Mathf.Lerp(spec.minSeparation, spec.maxSeparation, (float)rng.NextDouble());
            var bearing = (float)(rng.NextDouble() * 2.0 * Math.PI);
            var dir = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
            return new SpawnPoses
            {
                agentPos = arenaCenter - 0.5f * separation * dir,
                agentRotDeg = (float)(rng.NextDouble() * 360.0),
                baselinePos = arenaCenter + 0.5f * separation * dir,
                baselineRotDeg = (float)(rng.NextDouble() * 360.0),
            };
        }
    }

    /// <summary>Timestamped result sink under repo-root results/, shared by the tests and the RL hosts.</summary>
    public static class EpisodeJsonl
    {
        public static string NewRunPath(string tag, string folder = "rl-episodes")
        {
            // In a player Application.dataPath is the exe's Data dir, not the repo tree the editor layout climbs to.
            var baseDir = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."))
                : Application.persistentDataPath;
            var dir = Path.Combine(baseDir, "results", folder);
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(dir, $"{stamp}-{tag}.jsonl");
        }

        public static void Append(string path, in EpisodeResult result) =>
            File.AppendAllText(path, result.ToJsonLine() + "\n");
    }
}
