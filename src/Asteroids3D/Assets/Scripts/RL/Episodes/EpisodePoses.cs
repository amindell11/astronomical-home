using System;
using Utils;
using UnityEngine;
using RL.Reward;

namespace RL.Episodes
{
    [Serializable]
    public struct SpawnPoses
    {
        public Vector2 agentPos;
        public float agentRotDeg;
        public Vector2 baselinePos;
        public float baselineRotDeg;
    }

    public struct ExtraEnemyPose
    {
        public Vector2 pos;
        public float rotDeg;
    }

    /// <summary>Pure pose derivation from (runSeed, episodeIndex): separation band + bearing + facings, so any episode replays from its index.</summary>
    public static class EpisodePoses
    {
        public const float ExtraEnemyOffsetDeg = 30f;

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

        public static ExtraEnemyPose ExtraOnBaselineCircle(Vector2 referencePos, Vector2 baselinePos, int index)
        {
            if (index != 0 && index != 1)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    "a roster matrix places at most two extra enemies");
            var offset = (index == 0 ? ExtraEnemyOffsetDeg : -ExtraEnemyOffsetDeg) * Mathf.Deg2Rad;
            var delta = baselinePos - referencePos;
            var bearing = Mathf.Atan2(delta.y, delta.x);
            var angle = bearing + offset;
            var pos = referencePos + delta.magnitude * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var toReference = referencePos - pos;
            return new ExtraEnemyPose
            {
                pos = pos,
                rotDeg = Mathf.Atan2(-toReference.x, toReference.y) * Mathf.Rad2Deg,
            };
        }
    }
}
