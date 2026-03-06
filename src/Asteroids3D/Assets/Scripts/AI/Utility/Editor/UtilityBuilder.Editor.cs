#if UNITY_EDITOR || DEBUG
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AI.Utility
{
    public partial class UtilityBuilder
    {
        private readonly List<(string name, float value)> breakdown = new();
        private bool trackBreakdown = true;

        /// <summary>
        /// Structured access to the factor breakdown for logging and analysis.
        /// Each entry is (factorName, clampedValue) as fed into the geometric mean.
        /// </summary>
        public IReadOnlyList<(string name, float value)> Factors => breakdown;

        /// <summary>Final geometric-mean result after Build(). Zero before Build() is called.</summary>
        public float Result { get; private set; }

        partial void TrackBreakdown(string name, float value)
        {
            if (trackBreakdown)
                breakdown.Add((name, value));
        }

        partial void ClearBreakdown()
        {
            breakdown.Clear();
        }

        partial void LogBreakdown()
        {
            Result = count > 0 ? Mathf.Pow(product, 1f / count) : 0f;
        }

        public string GetBreakdown()
        {
            if (!trackBreakdown || breakdown.Count == 0)
                return $"Total: {(count > 0 ? Mathf.Pow(product, 1f / count) : 0f):F3}";

            var sb = new StringBuilder();
            foreach (var (name, value) in breakdown)
            {
                sb.Append($"{name}:{value:F2} | ");
            }
            var result = count > 0 ? Mathf.Pow(product, 1f / count) : 0f;
            sb.Append($"= {result:F3} (geomean of {count})");
            return sb.ToString();
        }
    }
}
#endif
