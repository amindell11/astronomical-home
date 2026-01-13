using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AI.Utility
{
    [System.Serializable]
    public struct FactorRange
    {
        public float AtLow;
        public float AtHigh;
        
        public FactorRange(float atLow, float atHigh)
        {
            AtLow = atLow;
            AtHigh = atHigh;
        }
        
        public FactorRange Inverted => new FactorRange(AtHigh, AtLow);
    }

    /// <summary>
    /// Fluent builder for composing utility scores using geometric mean.
    /// Each factor represents "what percentage of desire remains?" given some condition.
    /// Final score is the geometric mean of all factors, preventing both additive dilution and multiplicative collapse.
    /// </summary>
    public class UtilityBuilder
    {
        private float product = 1f;
        private int count = 0;
        
#if UNITY_EDITOR || DEBUG
        private readonly List<(string name, float value)> breakdown = new();
        private bool trackBreakdown = true;
#endif

        public UtilityBuilder Factor(string name, float value)
        {
            var clamped = Mathf.Clamp(value, 0.01f, 2f);
            product *= clamped;
            count++;
            Track(name, clamped);
            return this;
        }

        public UtilityBuilder Factor(string name, float input, FactorRange range)
        {
            var t = Mathf.Clamp01(input);
            t = t * t * (3f - 2f * t);
            var value = Mathf.Lerp(range.AtLow, range.AtHigh, t);
            return Factor(name, value);
        }

        /// <summary>
        /// Adds a factor only when condition is true. When false, factor is skipped entirely (not counted).
        /// Use for optional bonuses/penalties that only apply in specific situations.
        /// </summary>
        public UtilityBuilder FactorIf(bool condition, string name, float valueIfTrue)
        {
            if (!condition) return this;
            return Factor(name, valueIfTrue);
        }

        /// <summary>
        /// Adds a factor with different values for true/false. Both outcomes affect the score.
        /// Use for binary conditions where both states should push utility (e.g., hasLOS vs noLOS).
        /// Range: AtLow = value when false, AtHigh = value when true.
        /// </summary>
        public UtilityBuilder FactorBinary(bool condition, string name, FactorRange range)
        {
            return Factor(name, condition ? range.AtHigh : range.AtLow);
        }

        public float Build()
        {
            if (count == 0) return 0f;
            var result = Mathf.Clamp(Mathf.Pow(product, 1f / count), 0f, 2f);
#if UNITY_EDITOR || DEBUG
            Debug.Log($"Utility Breakdown: {GetBreakdown()}");
#endif
            return result;
        }

        public float BuildRaw()
        {
            if (count == 0) return 0f;
            return Mathf.Pow(product, 1f / count);
        }

        public void Clear()
        {
            product = 1f;
            count = 0;
#if UNITY_EDITOR || DEBUG
            breakdown.Clear();
#endif
        }

        public string GetBreakdown()
        {
#if UNITY_EDITOR || DEBUG
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
#else
            return string.Empty;
#endif
        }

        private void Track(string name, float value)
        {
#if UNITY_EDITOR || DEBUG
            if (trackBreakdown)
                breakdown.Add((name, value));
#endif
        }
    }
}
