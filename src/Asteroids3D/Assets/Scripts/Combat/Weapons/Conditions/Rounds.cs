using System;
using System.Collections.Generic;
using Combat.Weapons;
using UnityEngine;

namespace Combat.Weapons.Conditions
{
    /// <summary>
    /// What an ammo display renders: the read-only, event-driven face of <see cref="Rounds"/>.
    /// (Co-located with its sole implementer; C# disallows implementing an interface nested
    /// in the implementing class itself.)
    /// </summary>
    public interface IAmmoReadout : IWeaponReadout
    {
        int AmmoCount { get; }
        int MaxAmmo { get; }
        bool IsReloading { get; }
        float ReloadProgress { get; }
        event Action<int> OnAmmoCountChanged;
        event Action OnReloadStarted;
        event Action OnReloadCompleted;
    }

    public class Rounds : WeaponCondition, IAmmoReadout
    {
        public enum RefillMode { Magazine = 0, PerRound = 1 }

        [Header("Ammo System")]
        [SerializeField] private int maxAmmo = 4;

        [Tooltip("Seconds until spent rounds come back (see Refill). 0 = never; ammo only refills on Reset (ship respawn).")]
        [SerializeField, Min(0f)] private float reloadTime = 0f;

        [Tooltip("Magazine: the whole magazine refills Reload Time after it empties. " +
                 "PerRound: each round comes back Reload Time after it was fired.")]
        [SerializeField] private RefillMode refill = RefillMode.Magazine;

        public event Action<int> OnAmmoCountChanged;
        public event Action OnReloadStarted;
        public event Action OnReloadCompleted;

        public int AmmoCount { get; private set; }
        public int MaxAmmo => maxAmmo;
        public float ReloadTime => reloadTime;
        public RefillMode Refill => refill;
        public bool IsReloading { get; private set; }

        /// <summary>Seconds from the first round to the last when fired <paramref name="secondsBetweenShots"/> apart.</summary>
        public float DumpSeconds(float secondsBetweenShots) => (maxAmmo - 1) * secondsBetweenShots;

        /// <summary>Seconds from the last round of a dump until the next dump can start; infinite when rounds never refill.</summary>
        public float RecoverySeconds(float secondsBetweenShots)
        {
            if (reloadTime <= 0f) return float.PositiveInfinity;
            if (refill == RefillMode.Magazine) return reloadTime;

            // Rounds return in the spacing they were fired, the first one Reload Time after the first shot.
            return Mathf.Max(secondsBetweenShots, reloadTime - DumpSeconds(secondsBetweenShots));
        }

        public float ReloadProgress =>
            !IsReloading ? 0f
            : refill == RefillMode.PerRound ? Mathf.Clamp01(1f - (roundDueTimes.Peek() - regenClock) / reloadTime)
            : Mathf.Clamp01(reloadElapsed / reloadTime);

        private float reloadElapsed;

        // FIFO works because every round shares one duration: the front always lands first.
        private readonly Queue<float> roundDueTimes = new();
        private float regenClock;

        private void Awake()
        {
            Reset();
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// Advances the reload timer by <paramref name="dt"/> seconds. Production drives this each
        /// frame with <c>Time.deltaTime</c> (via <see cref="Update"/>); tests can drive it directly
        /// for deterministic, zero-wall-time coverage.
        /// </summary>
        public void Tick(float dt)
        {
            if (!IsReloading) return;

            if (refill == RefillMode.PerRound)
                TickPerRound(dt);
            else
                TickMagazine(dt);
        }

        private void TickMagazine(float dt)
        {
            reloadElapsed += dt;
            if (reloadElapsed < reloadTime) return;

            IsReloading = false;
            AmmoCount = maxAmmo;
            OnReloadCompleted?.Invoke();
            OnAmmoCountChanged?.Invoke(AmmoCount);
        }

        private void TickPerRound(float dt)
        {
            regenClock += dt;
            var restored = 0;
            while (roundDueTimes.Count > 0 && roundDueTimes.Peek() <= regenClock)
            {
                roundDueTimes.Dequeue();
                restored++;
            }
            if (restored == 0) return;

            AmmoCount += restored;
            if (roundDueTimes.Count == 0)
            {
                IsReloading = false;
                regenClock = 0f;
                OnReloadCompleted?.Invoke();
            }
            OnAmmoCountChanged?.Invoke(AmmoCount);
        }

        public override void Reset()
        {
            IsReloading = false;
            reloadElapsed = 0f;
            roundDueTimes.Clear();
            regenClock = 0f;
            AmmoCount = maxAmmo;
            OnAmmoCountChanged?.Invoke(AmmoCount);
        }

        public override bool CanFire()
        {
            return AmmoCount > 0;
        }

        public override void ProcessFire()
        {
            AmmoCount--;
            OnAmmoCountChanged?.Invoke(AmmoCount);
            if (reloadTime <= 0f) return;

            if (refill == RefillMode.PerRound)
            {
                roundDueTimes.Enqueue(regenClock + reloadTime);
                if (!IsReloading) StartReload();
            }
            else if (AmmoCount <= 0)
            {
                reloadElapsed = 0f;
                StartReload();
            }
        }

        private void StartReload()
        {
            IsReloading = true;
            OnReloadStarted?.Invoke();
        }

        /// <summary>
        /// Configures the magazine at runtime (weapon tuning / upgrades) and resets it to full.
        /// Serialized fields act as the authored inspector defaults.
        /// </summary>
        public void Configure(int maxAmmo, float reloadTime, RefillMode refill = RefillMode.Magazine)
        {
            this.maxAmmo = maxAmmo;
            this.reloadTime = reloadTime;
            this.refill = refill;
            Reset();
        }
    }
}
