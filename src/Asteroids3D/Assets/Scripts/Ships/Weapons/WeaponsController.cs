using UnityEngine;
using Weapons;

namespace Ships.Weapons
{
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] public WeaponComponent primary;
        [SerializeField] public WeaponComponent secondary;

        private void Awake()
        {
            primary = GetComponentInChildren<LaserGun>();
            secondary = GetComponentInChildren<Missiles>();
        }

        public void FirePrimary()
        {
            if (primary) primary.Fire();
        }

        public void FireSecondary()
        {
            if (secondary) secondary.Fire();
        }

        public void ResetSystem()
        {
            primary?.Reset();
            secondary?.Reset();
        }

        public void OnShipDeath()
        {
        }
    }
}
