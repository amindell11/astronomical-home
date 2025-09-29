using UnityEngine;

namespace Ships.Weapons.Conditions
{
    public class Rounds : WeaponCondition
    {
        [Header("Ammo System")]
        [SerializeField] private int maxAmmo = 4;
        
        public event System.Action<int> OnAmmoCountChanged;

        public int AmmoCount { get; private set; }
        public int MaxAmmo => maxAmmo;

        private void Start()
        {
            Reset();
        }

        public override void Reset()
        {
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
        }
    }
}
