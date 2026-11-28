using System;
using UnityEngine;

namespace Ships.Damage
{
    public class RegenResource : Resource
    {
        public event Action OnFullyRegenerated;
        public bool IsRegenerating => !held && CurrentValue < MaxValue && regenRate > 0f && clock >= lastDamageTime + regenDelay;
        private float regenRate;
        private float regenDelay;
        private float clock;
        private float lastDamageTime;
        private bool held;

        public RegenResource(float maxValue, float regenRate, float regenDelay) : base(maxValue)
        {
            this.regenRate = regenRate;
            this.regenDelay = regenDelay;
            lastDamageTime = -regenDelay; // Allows for immediate regeneration at the start
        }

        public override float ApplyDamage(float amount)
        {
            var damageAbsorbed = base.ApplyDamage(amount);
            if (damageAbsorbed > 0)
                lastDamageTime = clock;
            return damageAbsorbed;
        }

        public override void Reset()
        {
            base.Reset();
            clock = 0f;
            lastDamageTime = -regenDelay;
            held = false;
        }

        public void Update(float deltaTime, bool held)
        {
            this.held = held;
            clock += deltaTime;
            if (IsRegenerating)
                Regenerate(deltaTime);
        }
        
        private void Regenerate(float deltaTime)
        {
            var regenAmount = regenRate * deltaTime;
            Set(Mathf.Min(CurrentValue + regenAmount, MaxValue));
            if (CurrentValue >= MaxValue) OnFullyRegenerated?.Invoke();
        }
        
        public void Configure(float maxValue, float regenRate, float regenDelay)
        {
            base.Configure(maxValue);
            this.regenRate = regenRate;
            this.regenDelay = regenDelay;
        }

        public void Rescale(float maxValue, float regenRate, float regenDelay)
        {
            base.Rescale(maxValue);
            this.regenRate = regenRate;
            this.regenDelay = regenDelay;
        }
    }
}
