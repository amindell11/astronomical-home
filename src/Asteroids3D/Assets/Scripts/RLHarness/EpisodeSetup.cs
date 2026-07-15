using System;
using System.Globalization;
using System.IO;
using Combat.Projectile;
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

    /// <summary>Episode-boundary projectile flush: returns every in-flight projectile to its pool (never Destroy — pooled instances outlive ship death).</summary>
    public static class ProjectileFlush
    {
        public static int ReturnAllToPool()
        {
            var live = UnityEngine.Object.FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None);
            foreach (var projectile in live)
                projectile.ReturnToPoolImmediate();
            return live.Length;
        }

        public static int ActiveCount() =>
            UnityEngine.Object.FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None).Length;
    }

    /// <summary>Timestamped result sink under repo-root results/rl-episodes/, shared by the tests and the training host.</summary>
    public static class EpisodeJsonl
    {
        public static string NewRunPath(string tag)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            var dir = Path.Combine(repoRoot, "results", "rl-episodes");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(dir, $"{stamp}-{tag}.jsonl");
        }

        public static void Append(string path, in EpisodeResult result) =>
            File.AppendAllText(path, result.ToJsonLine() + "\n");
    }
}
