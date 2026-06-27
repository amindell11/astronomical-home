using System;
using UnityEngine;

namespace Combat.Conditions
{
    public class Cooldown : WeaponCondition
    {
        [SerializeField] private float fireRate = 0.2f;
        
        private float nextFireTime;
        private bool wasOnCooldown;

        public event Action OnCooldownStart;
        public event Action OnCooldownReady;
        
        public float CooldownRemaining => Mathf.Max(0, nextFireTime - Time.time);
        public float CooldownPercentage => fireRate > 0 ? CooldownRemaining / fireRate : 0f;

        private void Update()
        {
            var isOnCooldown = !CanFire();
            if (wasOnCooldown && !isOnCooldown)
            {
                OnCooldownReady?.Invoke();
            }
            wasOnCooldown = isOnCooldown;
        }

        public override bool CanFire()
        {
            return Time.time >= nextFireTime;
        }

        public override void ProcessFire()
        {
            nextFireTime = Time.time + fireRate;
            OnCooldownStart?.Invoke();
            wasOnCooldown = true;
        }

        public override void Reset()
        {
            nextFireTime = 0;
            wasOnCooldown = false;
        }
    }
}