using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Pure reward terms — f(prev, next) → float, no events, no scene (§3.3 of the tactical-AI roadmap).</summary>
    public static class RewardTerms
    {
        /// <summary>Delta-sampled dense term: λ·(enemy pool lost) − λ·(my pool lost), each normalized by that ship's own max pool; contributions telescope to the start-to-end pool swing.</summary>
        public static float PoolDifferential(in CombatSnapshot prev, in CombatSnapshot next, float lambda)
        {
            var enemyLost = (prev.enemyPool - next.enemyPool) / Mathf.Max(next.enemyMaxPool, 1e-6f);
            var myLost = (prev.myPool - next.myPool) / Mathf.Max(next.myMaxPool, 1e-6f);
            return lambda * enemyLost - lambda * myLost;
        }

        /// <summary>Sparse outcome spine: win +1, loss −1, draw 0.</summary>
        public static float Outcome(EpisodeOutcome outcome) => outcome switch
        {
            EpisodeOutcome.Win => 1f,
            EpisodeOutcome.Loss => -1f,
            _ => 0f,
        };
    }
}
