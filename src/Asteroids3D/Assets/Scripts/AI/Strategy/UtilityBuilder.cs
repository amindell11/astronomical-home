using UnityEngine;
#if UNITY_EDITOR || DEBUG
using System.Collections.Generic;
#endif

namespace AI.Utility
{
    [System.Serializable]
    public struct FactorRange
    {
        public float atLow;
        public float atHigh;
        
        public FactorRange(float atLow, float atHigh)
        {
            this.atLow = atLow;
            this.atHigh = atHigh;
        }
        
        public FactorRange Inverted => new FactorRange(atHigh, atLow);
    }
    
    public class UtilityBuilder
    {
        private float product = 1f;
        private float totalWeight = 0f;

        public UtilityBuilder Factor(string name, float value, float weight = 1f)
        {
            var clamped = Mathf.Clamp(value, 0.01f, 2f);
            if (weight <= 0f) return this;
            product *= Mathf.Pow(clamped, weight);
            totalWeight += weight;
#if UNITY_EDITOR || DEBUG
            if (trackBreakdown)
                breakdown.Add((name, clamped));
#endif
            return this;
        }

        public UtilityBuilder Factor(string name, float input, FactorRange range, float weight = 1f)
        {
            var t = Mathf.Clamp01(input);
            t = t * t * (3f - 2f * t);
            var value = Mathf.Lerp(range.atLow, range.atHigh, t);
            return Factor(name, value, weight);
        }

        public UtilityBuilder FactorIf(bool condition, string name, float valueIfTrue, float weight = 1f)
        {
            return !condition ? this : Factor(name, valueIfTrue, weight);
        }

        public UtilityBuilder FactorBinary(bool condition, string name, FactorRange range, float weight = 1f)
        {
            return Factor(name, condition ? range.atHigh : range.atLow, weight);
        }

        public float Build()
        {
            if (totalWeight <= 0f) return 0f;
            var result = Mathf.Clamp(Mathf.Pow(product, 1f / totalWeight), 0f, 2f);
#if UNITY_EDITOR || DEBUG
            Result = Mathf.Pow(product, 1f / totalWeight);
#endif
            return result;
        }

        public void Clear()
        {
            product = 1f;
            totalWeight = 0f;
#if UNITY_EDITOR || DEBUG
            breakdown.Clear();
#endif
        }

#if UNITY_EDITOR || DEBUG
        private readonly List<(string name, float value)> breakdown = new();
        private bool trackBreakdown = true;

        /// <summary>Factor breakdown for logging/analysis: each entry is (factorName, clampedValue) as fed into the geometric mean.</summary>
        public IReadOnlyList<(string name, float value)> Factors => breakdown;

        /// <summary>Final geometric-mean result after Build(). Zero before Build() is called.</summary>
        public float Result { get; private set; }
#endif
    }
}
