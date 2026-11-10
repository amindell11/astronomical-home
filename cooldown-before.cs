using Balance;
using System;
using Combat.Weapons.Mods;
using UnityEngine;

namespace Combat.Weapons.Conditions
{
    public class Cooldown : WeaponCondition
    {
        [Stat, SerializeField] private float fireRate = 0.2f;

        // Internal dt-driven clock: resets with the weapon, so pacing replays identically regardless of absolute session time. Ticks on fixed steps because firing does.
        private float clock;
        private float nextFireTime;
        private bool wasOnCooldown;

        public event Action OnCooldownStart;
        public event Action OnCooldownReady;

        public float SecondsBetweenShots => mods.Apply(WeaponModField.FireRate, fireRate);
        public float CooldownRemaining => Mathf.Max(0, nextFireTime - clock);
        public float CooldownPercentage => SecondsBetweenShots > 0 ? CooldownRemaining / SecondsBetweenShots : 0f;

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
            return clock >= nextFireTime - HalfStep;
        }

        public override void ProcessFire()
        {
            nextFireTime = clock + SecondsBetweenShots;
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
