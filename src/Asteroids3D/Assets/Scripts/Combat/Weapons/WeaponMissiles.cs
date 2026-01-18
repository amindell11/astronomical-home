using Combat.Conditions;
using Combat.Projectile;
using Combat.Targeting;
using UnityEngine;
using Missile = Combat.Projectile.Missile;

namespace Combat.Weapons
{
    public partial class WeaponMissiles : WeaponBase<Missile>
    {
        [Header("Targeting")]
        [SerializeField] private TargetingComputer targetingComputer;

        public TargetingComputer Targeting => targetingComputer;

        private Rounds rounds;

        protected override void Awake()
        {
            base.Awake();
            rounds = GetComponent<Rounds>();
        }

        public override ProjectileBase Fire()
        {
            var proj = base.Fire() as Missile;

            if (!proj) return null;

            var lockedTarget = targetingComputer.ConsumeLock();
            if (lockedTarget != null)
                proj.SetTarget(lockedTarget.TargetPoint);
        
            return proj;
        }
    }
} 
