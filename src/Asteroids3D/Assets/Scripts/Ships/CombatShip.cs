using Combat;
using Combat.Targeting;
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
        }

        protected override void UpdateState()
        {
            base.UpdateState();
            var s = CurrentState;
            s.isPrimaryReady = Weapons?.Primary?.CanFire() ?? false;
            s.isSecondaryReady = Weapons?.Secondary?.CanFire() ?? false;
            CurrentState = s;
        }

        protected override void ExecuteCommand()
        {
            if (CurrentCommand.primaryFire)
                Weapons?.FirePrimary();
            if (CurrentCommand.secondaryFire)
                Weapons?.FireSecondary();
        }

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
