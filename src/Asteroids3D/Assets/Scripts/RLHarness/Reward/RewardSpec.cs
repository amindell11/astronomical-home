using System;

namespace Game.RLHarness
{
    /// <summary>The single reward/episode config, embedded verbatim in every result row so JSONL output is self-describing.</summary>
    [Serializable]
    public struct RewardSpec
    {
        public float lambda;
        public float envelopeK1;
        public float envelopeK2;
        public float borderKb;
        public float gamma;
        public int decisionIntervalSteps;
        public int timeoutDecisions;
        public float arenaRadius;
        public float borderSoftFraction;
        public float minSeparation;
        public float maxSeparation;
        public int runSeed;

        public static RewardSpec Default => new()
        {
            lambda = 1f,
            envelopeK1 = 0.1f,
            envelopeK2 = 0.1f,
            borderKb = 0.5f,
            gamma = 0.99f,
            decisionIntervalSteps = 10,
            timeoutDecisions = 600,
            arenaRadius = 120f,
            borderSoftFraction = 0.8f,
            minSeparation = 25f,
            maxSeparation = 60f,
            runSeed = 1,
        };
    }
}
