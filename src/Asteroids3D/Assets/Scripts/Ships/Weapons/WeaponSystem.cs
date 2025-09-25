using UnityEngine;
using Weapons;

namespace Ships.Weapons
{
    public class WeaponSystem : MonoBehaviour
    {
        public LaserGun LaserGun { get; private set; }
        public MissileLauncher MissileLauncher { get; private set; }

        private void Awake()
        {
            LaserGun = GetComponentInChildren<LaserGun>();
            MissileLauncher = GetComponentInChildren<MissileLauncher>();
        }

        public void FirePrimary()
        {
            if (LaserGun) LaserGun.Fire();
        }

        public void FireSecondary()
        {
            if (MissileLauncher) MissileLauncher.Fire();
        }

        public void ResetSystem()
        {
            LaserGun?.ResetHeat();
            MissileLauncher?.ReplenishAmmo();
        }

        public void OnShipDeath()
        {
            MissileLauncher?.CancelLock();
        }
    }
}
