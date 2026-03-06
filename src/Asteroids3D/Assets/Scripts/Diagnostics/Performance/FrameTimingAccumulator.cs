using System.Diagnostics;

namespace Diagnostics.Performance
{
    /// <summary>
    /// Lightweight per-frame timing accumulator using Stopwatch.
    /// Each system calls Begin/End around its hot path. The collector
    /// reads and resets totals once per frame.
    /// </summary>
    public static class FrameTimingAccumulator
    {
        public enum Category
        {
            AIUpdate,
            ScoutScan,
            ProjectileFire,
            ProjectileUpdate,
            ObjectiveTracker,
            AsteroidSpawn,
            AsteroidUpdate,
            Count
        }

        private static readonly long[] ticks = new long[(int)Category.Count];
        private static readonly Stopwatch sw = new();

        private static Category activeCategory;
        private static bool active;

        public static void Begin(Category cat)
        {
            activeCategory = cat;
            active = true;
            sw.Restart();
        }

        public static void End()
        {
            if (!active) return;
            sw.Stop();
            ticks[(int)activeCategory] += sw.ElapsedTicks;
            active = false;
        }

        /// <summary>
        /// Read accumulated milliseconds for a category and reset it.
        /// Call once per frame from the collector.
        /// </summary>
        public static double ReadAndResetMs(Category cat)
        {
            var idx = (int)cat;
            var t = ticks[idx];
            ticks[idx] = 0;
            return t * 1000.0 / Stopwatch.Frequency;
        }
    }
}
