using Combat.Weapons;
using UnityEngine;
using Weapons;

namespace Ships.Weapons
{
    public class WeaponsController : MonoBehaviour
    {
        [SerializeField] private WeaponComponent primaryMount;
        [SerializeField] private WeaponComponent secondaryMount;
        public WeaponComponent Primary { get; private set; }
        public WeaponComponent Secondary { get; private set; }

        private void Awake()
        {
            if (primaryMount) Primary = Instantiate(primaryMount, transform);
            if (secondaryMount) Secondary = Instantiate(secondaryMount, transform);
        }

        public void FirePrimary()
        {
            if (Primary) Primary.Fire();
        }

        public void FireSecondary()
        {
            if (Secondary) Secondary.Fire();
        }

        public void ResetSystem()
        {
            Primary?.Reset();
            Secondary?.Reset();
        }

        public void OnShipDeath()
        {
            ResetSystem();
        }
    }
}
