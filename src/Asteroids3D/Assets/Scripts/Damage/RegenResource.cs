using UnityEngine;

namespace Ships.Damage
{
    public class RegenResource : Resource
    {
        private float regenRate;
        private float regenDelay;
        private float clock;            // internal time base, advanced by Update(dt)
        private float lastDamageTime;

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
        }

        public void Update(float deltaTime)
        {
            clock += deltaTime;
            if (CurrentValue < MaxValue && clock >= lastDamageTime + regenDelay)
                Regenerate(deltaTime);
        }
        
        private void Regenerate(float deltaTime)
        {
            var regenAmount = regenRate * deltaTime;
            Set(Mathf.Min(CurrentValue + regenAmount, MaxValue));
        }
        
        public void Configure(float maxValue, float regenRate, float regenDelay)
        {
            base.Configure(maxValue);
            this.regenRate = regenRate;
            this.regenDelay = regenDelay;
        }
    }
}
