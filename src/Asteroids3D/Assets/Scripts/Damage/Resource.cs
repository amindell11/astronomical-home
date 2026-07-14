using System;
using UnityEngine;

namespace Ships.Damage
{
    public class Resource
    {
        public event Action<float, float, float> OnValueChanged; // current, previous, max
        
        public float CurrentValue { get; protected set; }
        public float MaxValue { get; private set; }
        
        public float Pct => MaxValue > 0 ? CurrentValue / MaxValue : 0f;

        public Resource(float maxValue)
        {
            MaxValue = maxValue;
            CurrentValue = MaxValue;
        }

        /// <summary>
        /// Applies damage to this resource and returns the amount absorbed.
        /// </summary>
        /// <returns>Amount of damage absorbed by this resource.</returns>
        public virtual float ApplyDamage(float amount)
        {
            if (amount <= 0 || CurrentValue <= 0) return 0;

            var prev = CurrentValue;
            var damageAbsorbed = Mathf.Min(amount, CurrentValue);
            CurrentValue -= damageAbsorbed;
            
            OnValueChanged?.Invoke(CurrentValue, prev, MaxValue);
            return damageAbsorbed;
        }

        public virtual void Reset()
        {
            var prev = CurrentValue;
            CurrentValue = MaxValue;
            OnValueChanged?.Invoke(CurrentValue, prev, MaxValue);
        }

        public void Configure(float maxValue)
        {
            MaxValue = maxValue;
            Reset();
        }

        protected void Set(float val)
        {
            var prev = CurrentValue;
            CurrentValue = val;
            OnValueChanged?.Invoke(CurrentValue, prev, MaxValue);
        }
    }
}
