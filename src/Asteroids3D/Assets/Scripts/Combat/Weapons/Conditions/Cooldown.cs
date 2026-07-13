using System;
using UnityEngine;

namespace Combat.Conditions
{
    public class Cooldown : WeaponCondition
    {
        [SerializeField] private float fireRate = 0.2f;

        // Internal dt-driven clock: resets with the weapon, so pacing replays identically regardless of absolute session time. Ticks on fixed steps because firing does.
        private float clock;
        private float nextFireTime;
        private bool wasOnCooldown;

        public event Action OnCooldownStart;
        public event Action OnCooldownReady;

        public float SecondsBetweenShots => fireRate;
        public float CooldownRemaining => Mathf.Max(0, nextFireTime - clock);
        public float CooldownPercentage => fireRate > 0 ? CooldownRemaining / fireRate : 0f;

        private void FixedUpdate()
        {
            clock += Time.fixedDeltaTime;
        }

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
            return clock >= nextFireTime;
        }

        public override void ProcessFire()
        {
            nextFireTime = clock + fireRate;
            OnCooldownStart?.Invoke();
            wasOnCooldown = true;
        }

        public override void Reset()
        {
            clock = 0;
            nextFireTime = 0;
            wasOnCooldown = false;
        }
    }
}
