using System;
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
}
