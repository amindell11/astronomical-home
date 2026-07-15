using System.Globalization;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The frozen arc-gate eval constants (doc/Feature_Plans/RL_MLAgents_Agent.md decision 8): training seed, the 20 pinned held-out seeds (disjoint from training; never a tuning set), the pinned inference seed, and the Wilson bound the gate reads.</summary>
    public static class EvalProtocol
    {
        public const int TrainingRunSeed = 1;
        public const int InferenceSeed = 20260715;

        public static readonly int[] HeldOutSeeds =
        {
            1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010,
            1011, 1012, 1013, 1014, 1015, 1016, 1017, 1018, 1019, 1020,
        };

        /// <summary>Seed-list selection for eval runs ("train" = checkpoint selection on the training seed BEFORE the held-out set opens): empty/"held-out" → the pinned list, "train" → the training run seed, else an explicit comma list. The tag labels the run's artifacts so train and held-out evals can never be confused.</summary>
        public static int[] ResolveSeeds(string selector, out string tag)
        {
            if (string.IsNullOrEmpty(selector) || selector == "held-out")
            {
                tag = "held-out";
                return HeldOutSeeds;
            }
            if (selector == "train")
            {
                tag = "train";
                return new[] { TrainingRunSeed };
            }

            var parts = selector.Split(',');
            var seeds = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                seeds[i] = int.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
            tag = "custom";
            return seeds;
        }

        /// <summary>Wilson 95% lower confidence bound on a binomial proportion; callers count draws as non-wins.</summary>
        public static float WilsonLowerBound(int successes, int trials)
        {
            if (trials <= 0) return 0f;
            const float z = 1.959964f;
            var n = (float)trials;
            var p = successes / n;
            var z2 = z * z;
            var center = p + z2 / (2f * n);
            var margin = z * Mathf.Sqrt(p * (1f - p) / n + z2 / (4f * n * n));
            return Mathf.Max(0f, (center - margin) / (1f + z2 / n));
        }
    }
}
