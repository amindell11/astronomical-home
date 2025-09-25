using UnityEngine;

namespace Ships
{
    public class RegeneratingDamageResource : DamageResource
    {
        private float regenRate;
        private float regenDelay;
        private float lastDamageTime;

        public RegeneratingDamageResource(float maxValue, float regenRate, float regenDelay) : base(maxValue)
        {
            this.regenRate = regenRate;
            this.regenDelay = regenDelay;
            lastDamageTime = -regenDelay; // Allows for immediate regeneration at the start
        }

        public override float ApplyDamage(float amount)
        {
            float appliedDamage = base.ApplyDamage(amount);
            if (appliedDamage > 0)
                lastDamageTime = Time.time;
            return appliedDamage;
        }
        
        public void Update(float deltaTime)
        {
            if (CurrentValue < MaxValue && Time.time >= lastDamageTime + regenDelay)
                Regenerate(deltaTime);
        }
        
        private void Regenerate(float deltaTime)
        {
            float regenAmount = regenRate * deltaTime;
            Set(CurrentValue+regenAmount);
        }
        
        public void Configure(float maxValue, float regenRate, float regenDelay)
        {
            base.Configure(maxValue);
            this.regenRate = regenRate;
            this.regenDelay = regenDelay;
        }
    }
}
