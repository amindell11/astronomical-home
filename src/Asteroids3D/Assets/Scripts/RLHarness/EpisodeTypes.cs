using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.RLHarness
{
    public enum EpisodeOutcome { Unresolved, Win, Loss, Draw }

    /// <summary>How a paid decision boundary relates to the episode's end: Terminal = game-semantic end (death/out-of-bounds, Φ forced to 0), Truncation = harness cutoff (timeout, Φ kept so the value function bootstraps past it).</summary>
    public enum EndKind { None, Terminal, Truncation }

    /// <summary>One paid decision boundary's reward decomposition; the Finish-computed outcome rides the terminal boundary, never a separate channel.</summary>
    public struct BoundaryResult
    {
        public int decision;
        public float dense;
        public float shapingEnvelope;
        public float shapingBorder;
        public float outcomeReward;
        public EndKind endKind;

        public float Total => dense + shapingEnvelope + shapingBorder + outcomeReward;
    }

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
        public string endKind;
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
        // Non-zero only on truncation: the kept final Φ the shaping sum telescopes to (terminal forces Φ = 0).
        public float endPhiEnvelope;
        public float endPhiBorder;

        public RewardSpec spec;
        public List<DecisionRow> trace;

        public const string SchemaId = "rl-episode-v2";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }
}
