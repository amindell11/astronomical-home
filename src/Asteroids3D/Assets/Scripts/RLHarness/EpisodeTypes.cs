using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.RLHarness
{
    public enum EpisodeOutcome { Unresolved, Win, Loss, Draw }

    /// <summary>Pure termination rules over one snapshot: first death (mutual kill = loss), agent out-of-bounds = loss, baseline out-of-bounds = draw + anomaly.</summary>
    public static class EpisodeRules
    {
        public struct Verdict
        {
            public EpisodeOutcome outcome;
            public bool mutualKill;
            public bool agentExitedBounds;
            public bool baselineExitedBounds;
        }

        public static Verdict Evaluate(in CombatSnapshot s, in RewardSpec spec)
        {
            if (!s.myAlive && !s.enemyAlive)
                return new Verdict { outcome = EpisodeOutcome.Loss, mutualKill = true };
            if (!s.myAlive)
                return new Verdict { outcome = EpisodeOutcome.Loss };
            if (!s.enemyAlive)
                return new Verdict { outcome = EpisodeOutcome.Win };
            if (s.myDistFromCenter > spec.arenaRadius)
                return new Verdict { outcome = EpisodeOutcome.Loss, agentExitedBounds = true };
            if (s.enemyDistFromCenter > spec.arenaRadius)
                return new Verdict { outcome = EpisodeOutcome.Draw, baselineExitedBounds = true };
            return new Verdict { outcome = EpisodeOutcome.Unresolved };
        }
    }

    [Serializable]
    public struct DecisionRow
    {
        public int decision;
        public float dense;
        public float shapingEnvelope;
        public float shapingBorder;
        public float phiEnvelope;
        public float phiBorder;
    }

    [Serializable]
    public struct EpisodeResult
    {
        public string schema;
        public int episodeIndex;
        public string outcome;
        public bool mutualKill;
        public bool agentExitedBounds;
        public bool baselineExitedBounds;
        public bool timedOut;
        public int decisions;
        public float simSeconds;

        public float sumDense;
        public float sumShapingEnvelope;
        public float sumShapingBorder;
        public float outcomeReward;
        public float totalReward;

        public float startMyPool;
        public float startEnemyPool;
        public float endMyPool;
        public float endEnemyPool;
        public float startPhiEnvelope;
        public float startPhiBorder;
        public float midPhiEnvelopeSum;
        public float midPhiBorderSum;

        public RewardSpec spec;
        public List<DecisionRow> trace;

        public const string SchemaId = "rl-episode-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }
}
