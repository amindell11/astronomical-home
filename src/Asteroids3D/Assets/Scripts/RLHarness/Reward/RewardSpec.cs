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
        // Flat cost per paid decision: a timeout draw nets −timeCostPerDecision·timeoutDecisions, so waiting is never free.
        public float timeCostPerDecision;
        public float gamma;
        public int decisionIntervalSteps;
        public int timeoutDecisions;
        public float arenaRadius;
        public float borderSoftFraction;
        public float minSeparation;
        public float maxSeparation;
        public int runSeed;
        // Asteroid-field episodes (default OFF: an empty arena, byte-identical to the pre-field harness).
        public bool useAsteroidField;
        public float fieldDensityScale;
        public float collisionLethality;
        // Opponent mixture weights (relative — OpponentRoster.Pick normalizes implicitly).
        public float weightAggressor;
        public float weightEvader;
        public float weightOrbiter;
        public float weightKiter;
        public float weightDummy;

        public static RewardSpec Default => new()
        {
            lambda = 1f,
            envelopeK1 = 0.1f,
            envelopeK2 = 0.1f,
            borderKb = 0.5f,
            timeCostPerDecision = 5e-4f,
            gamma = 0.99f,
            decisionIntervalSteps = ShipCombatPolicy.DecisionIntervalSteps,
            timeoutDecisions = 600,
            arenaRadius = 120f,
            borderSoftFraction = 0.8f,
            minSeparation = 25f,
            maxSeparation = 60f,
            runSeed = 1,
            useAsteroidField = false,
            fieldDensityScale = 1f,
            collisionLethality = 1f,
            weightAggressor = 0.4f,
            weightEvader = 0.2f,
            weightOrbiter = 0.15f,
            weightKiter = 0.15f,
            weightDummy = 0.1f,
        };
    }
}
