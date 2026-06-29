using Combat;
using Combat.Targeting;
using Ships.Command;
using Ships.Weapons;
using UnityEngine;

namespace Ships
{
    [RequireComponent(typeof(WeaponsController))]
    public class CombatShip : Ship, IShooter
    {
        public WeaponsController Weapons { get; private set; }
        public LockOnSensor Targeting { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Weapons = GetComponent<WeaponsController>();
            Targeting = GetComponentInChildren<LockOnSensor>();
            Weapons.Initialize(() => Kinematics);
        }

        protected override ShipControl BuildShipControl() =>
            new(this, Movement, Weapons.Context, Weapons);

        protected override void HandleShipDeath()
        {
            Weapons?.OnShipDeath();
            base.HandleShipDeath();
        }

        public override void ResetShip()
        {
            Weapons?.ResetSystem();
            base.ResetShip();
        }
    }
}
