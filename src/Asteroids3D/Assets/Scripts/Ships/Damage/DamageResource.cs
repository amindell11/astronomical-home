using System;
using UnityEngine;

namespace Ships
{
    public class DamageResource
    {
        public event Action<float, float, float> OnValueChanged; // current, previous, max
        
        public float CurrentValue { get; protected set; }
        public float MaxValue { get; private set; }
        
        public float Pct => MaxValue > 0 ? CurrentValue / MaxValue : 0f;

        public DamageResource(float maxValue)
        {
            MaxValue = maxValue;
            CurrentValue = MaxValue;
        }

        public virtual float ApplyDamage(float amount)
        {
            if (amount <= 0 || CurrentValue <= 0) return 0;

            float prev = CurrentValue;
            float damageToApply = Mathf.Min(amount, CurrentValue);
            CurrentValue -= damageToApply;
            
            OnValueChanged?.Invoke(CurrentValue, prev, MaxValue);
            return damageToApply;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void Reset()
        {
            float prev = CurrentValue;
            CurrentValue = MaxValue;
            OnValueChanged?.Invoke(CurrentValue, prev, MaxValue);
        }

        public void Configure(float maxValue)
        {
            MaxValue = maxValue;
            Reset();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        protected void Set(float val)
        {
            var prev = CurrentValue;
            CurrentValue = val;
            OnValueChanged?.Invoke(CurrentValue, prev, MaxValue);
        }
    }
}
