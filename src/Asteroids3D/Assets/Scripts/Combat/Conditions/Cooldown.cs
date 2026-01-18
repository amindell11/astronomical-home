using System;
using UnityEngine;

namespace Combat.Conditions
{
    public class Cooldown : WeaponCondition
    {
        [SerializeField] private float fireRate = 0.2f;
        
        private float _nextFireTime;
        private bool _wasOnCooldown;

        public event Action OnCooldownStart;
        public event Action OnCooldownReady;
        
        public float CooldownRemaining => Mathf.Max(0, _nextFireTime - Time.time);
        public float CooldownPercentage => fireRate > 0 ? CooldownRemaining / fireRate : 0f;

        private void Update()
        {
            var isOnCooldown = !CanFire();
            if (_wasOnCooldown && !isOnCooldown)
            {
                OnCooldownReady?.Invoke();
            }
            _wasOnCooldown = isOnCooldown;
        }

        public override bool CanFire()
        {
            return Time.time >= _nextFireTime;
        }

        public override void ProcessFire()
        {
            _nextFireTime = Time.time + fireRate;
            OnCooldownStart?.Invoke();
            _wasOnCooldown = true;
        }

        public override void Reset()
        {
            _nextFireTime = 0;
            _wasOnCooldown = false;
        }
    }
}