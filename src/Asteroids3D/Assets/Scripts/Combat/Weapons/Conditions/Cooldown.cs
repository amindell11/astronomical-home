using Balance;
using System;
using Combat.Weapons.Mods;
using UnityEngine;

namespace Combat.Weapons.Conditions
{
    public class Cooldown : WeaponCondition
    {
        [Tooltip("Seconds between shots (at full spin when spin-up is on).")]
        [Stat, SerializeField] private float fireRate = 0.2f;

        [Header("Spin-up")]
        [Tooltip("Seconds of continuous fire from cold to full spin. Zero disables spin-up.")]
        [Stat, SerializeField, Min(0f)] private float spinUpTime = 0f;
        [Tooltip("Seconds between shots when cold.")]
        [Stat, SerializeField, Min(0f)] private float coldFireRate = 0.2f;
        [Tooltip("Seconds from full spin to cold once fire stops.")]
        [Stat, SerializeField, Min(0f)] private float spinDownTime = 0.4f;

        // Resettable fixed-step time keeps firing independent of the session's absolute clock.
        private float clock;
        private float nextFireTime;
        private bool hasFired;
        private bool wasOnCooldown;

        public event Action OnCooldownStart;
        public event Action OnCooldownReady;

        // Prefab assets skip Awake, so a disabled spin-up reads the authored rate.
        public float SecondsBetweenShots => mods.Apply(
            WeaponModField.FireRate,
            spinUpTime > 0f ? Mathf.Lerp(coldFireRate, fireRate, Spin) : fireRate);
        public float CooldownRemaining => Mathf.Max(0, nextFireTime - clock);
        public float CooldownPercentage => SecondsBetweenShots > 0 ? CooldownRemaining / SecondsBetweenShots : 0f;
        public bool StartsNewCadence { get; private set; }
        public float Spin { get; private set; }

        private void Awake() => Spin = spinUpTime > 0f ? 0f : 1f;

        private void FixedUpdate() => Tick(Time.fixedDeltaTime);

        public void Tick(float dt)
        {
            clock += dt;
            if (spinUpTime <= 0f)
            {
                Spin = 1f;
                return;
            }

            // Cadence still open, so spin follows shots rather than the trigger.
            var firing = hasFired && clock <= nextFireTime + dt;
            if (firing)
                Spin = Mathf.Min(1f, Spin + dt / spinUpTime);
            else if (spinDownTime > 0f)
                Spin = Mathf.Max(0f, Spin - dt / spinDownTime);
            else
                Spin = 0f;
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
            Spin = spinUpTime > 0f ? 0f : 1f;
        }
    }
}
