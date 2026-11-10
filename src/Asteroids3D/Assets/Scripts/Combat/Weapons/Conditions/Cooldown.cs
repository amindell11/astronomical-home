using Balance;
using System;
using Combat.Weapons.Mods;
using UnityEngine;

namespace Combat.Weapons.Conditions
{
    public class Cooldown : WeaponCondition
    {
        [Stat, SerializeField] private float fireRate = 0.2f;

        // Resettable fixed-step time keeps firing independent of the session's absolute clock.
        private float clock;
        private float nextFireTime;
        private bool hasFired;
        private bool wasOnCooldown;

        public event Action OnCooldownStart;
        public event Action OnCooldownReady;

        public float SecondsBetweenShots => mods.Apply(WeaponModField.FireRate, fireRate);
        public float CooldownRemaining => Mathf.Max(0, nextFireTime - clock);
        public float CooldownPercentage => SecondsBetweenShots > 0 ? CooldownRemaining / SecondsBetweenShots : 0f;
        public bool StartsNewCadence { get; private set; }

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
            StartsNewCadence = !hasFired || clock > nextFireTime + Time.fixedDeltaTime;
            nextFireTime = (StartsNewCadence ? clock : nextFireTime) + SecondsBetweenShots;
            hasFired = true;
            OnCooldownStart?.Invoke();
            wasOnCooldown = true;
        }

        public override void Reset()
        {
            clock = 0;
            nextFireTime = 0;
            hasFired = false;
            wasOnCooldown = false;
            StartsNewCadence = false;
        }
    }
}
