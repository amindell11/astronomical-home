namespace Game.RLHarness
{
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
}
