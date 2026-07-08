using System;
using UnityEngine;

namespace Combat.Conditions
{
    public class Rounds : WeaponCondition
    {
        [Header("Ammo System")]
        [SerializeField] private int maxAmmo = 4;

        [Tooltip("Seconds to auto-refill the magazine after it empties. 0 = never; ammo only refills on Reset (ship respawn).")]
        [SerializeField, Min(0f)] private float reloadTime = 0f;

        public event Action<int> OnAmmoCountChanged;
        public event Action OnReloadStarted;
        public event Action OnReloadCompleted;

        public int AmmoCount { get; private set; }
        public int MaxAmmo => maxAmmo;
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? Mathf.Clamp01(reloadElapsed / reloadTime) : 0f;

        private float reloadElapsed;

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

            reloadElapsed += dt;
            if (reloadElapsed < reloadTime) return;

            IsReloading = false;
            AmmoCount = maxAmmo;
            OnReloadCompleted?.Invoke();
            OnAmmoCountChanged?.Invoke(AmmoCount);
        }

        public override void Reset()
        {
            IsReloading = false;
            reloadElapsed = 0f;
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

            if (AmmoCount <= 0 && reloadTime > 0f)
            {
                IsReloading = true;
                reloadElapsed = 0f;
                OnReloadStarted?.Invoke();
            }
        }

        /// <summary>
        /// Configures the magazine at runtime (weapon tuning / upgrades) and resets it to full.
        /// Serialized fields act as the authored inspector defaults.
        /// </summary>
        public void Configure(int maxAmmo, float reloadTime)
        {
            this.maxAmmo = maxAmmo;
            this.reloadTime = reloadTime;
            Reset();
        }
    }
}
