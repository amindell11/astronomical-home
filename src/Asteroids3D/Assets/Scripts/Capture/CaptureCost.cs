using System.Collections.Generic;
using UnityEngine;

namespace Game.Capture
{
    /// <summary>Both backends sample every fixed step of a filmed episode, captured or skipped, so two clips of one episode spec compare directly.</summary>
    public sealed class CaptureCost
    {
        private readonly List<float> stepMs = new();
        private double previous = -1d;

        public int Steps => stepMs.Count;

        public void Sample()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (previous >= 0d) stepMs.Add((float)((now - previous) * 1000d));
            previous = now;
        }

        public float MeanMs()
        {
            if (stepMs.Count == 0) return 0f;
            var total = 0d;
            foreach (var ms in stepMs) total += ms;
            return (float)(total / stepMs.Count);
        }

        /// <summary>Per-step shape, not a cross-backend number: the painter path works only on captured steps, so its median lands on a skipped one. Backends compare on <see cref="MeanMs"/>.</summary>
        public float MedianMs()
        {
            if (stepMs.Count == 0) return 0f;
            var sorted = new List<float>(stepMs);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }
    }
}
