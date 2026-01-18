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
        private int count = 0;

        public UtilityBuilder Factor(string name, float value)
        {
            var clamped = Mathf.Clamp(value, 0.01f, 2f);
            product *= clamped;
            count++;
            TrackBreakdown(name, clamped);
            return this;
        }

        public UtilityBuilder Factor(string name, float input, FactorRange range)
        {
            var t = Mathf.Clamp01(input);
            t = t * t * (3f - 2f * t);
            var value = Mathf.Lerp(range.atLow, range.atHigh, t);
            return Factor(name, value);
        }
        
        public UtilityBuilder FactorIf(bool condition, string name, float valueIfTrue)
        {
            return !condition ? this : Factor(name, valueIfTrue);
        }

        public UtilityBuilder FactorBinary(bool condition, string name, FactorRange range)
        {
            return Factor(name, condition ? range.atHigh : range.atLow);
        }

        public float Build()
        {
            if (count == 0) return 0f;
            var result = Mathf.Clamp(Mathf.Pow(product, 1f / count), 0f, 2f);
            LogBreakdown();
            return result;
        }

        public float BuildRaw()
        {
            return count == 0 ? 0f : Mathf.Pow(product, 1f / count);
        }

        public void Clear()
        {
            product = 1f;
            count = 0;
            ClearBreakdown();
        }

        // Partial methods for editor-only functionality
        partial void TrackBreakdown(string name, float value);
        partial void ClearBreakdown();
        partial void LogBreakdown();
    }
}
