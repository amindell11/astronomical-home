using UnityEngine;

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
    
    public partial class UtilityBuilder
    {
        private float product = 1f;
        private float totalWeight = 0f;

        public UtilityBuilder Factor(string name, float value, float weight = 1f)
        {
            var clamped = Mathf.Clamp(value, 0.01f, 2f);
            if (weight <= 0f) return this;
            product *= Mathf.Pow(clamped, weight);
            totalWeight += weight;
            TrackBreakdown(name, clamped);
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
            LogBreakdown();
            return result;
        }

        public float BuildRaw()
        {
            return totalWeight <= 0f ? 0f : Mathf.Pow(product, 1f / totalWeight);
        }

        public void Clear()
        {
            product = 1f;
            totalWeight = 0f;
            ClearBreakdown();
        }

        // Partial methods for editor-only functionality
        partial void TrackBreakdown(string name, float value);
        partial void ClearBreakdown();
        partial void LogBreakdown();
    }
}
