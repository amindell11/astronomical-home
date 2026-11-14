using System;

namespace RL.Reward
{
    /// <summary>The single episode config, embedded verbatim in every result row so JSONL output is self-describing.</summary>
    [Serializable]
    public struct RewardSpec
    {
        public int decisionIntervalSteps;
        public int timeoutDecisions;
        public float arenaRadius;
        public float minSeparation;
        public float maxSeparation;
        public int runSeed;

        public static RewardSpec Default => new()
        {
            decisionIntervalSteps = 10,
            timeoutDecisions = 600,
            arenaRadius = 120f,
            minSeparation = 25f,
            maxSeparation = 60f,
            runSeed = 1,
        };
    }
}
