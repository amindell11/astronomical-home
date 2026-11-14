using System.Globalization;

namespace RL.Hosts
{
    /// <summary>The harness seed selection: the training seed and the 20 pinned held-out seeds, disjoint from it.</summary>
    public static class EvalProtocol
    {
        public const int TrainingRunSeed = 1;

        public static readonly int[] HeldOutSeeds =
        {
            1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010,
            1011, 1012, 1013, 1014, 1015, 1016, 1017, 1018, 1019, 1020,
        };

        /// <summary>The tag labels the run's artifacts so train, held-out and custom seed runs can never be confused.</summary>
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
    }
}
