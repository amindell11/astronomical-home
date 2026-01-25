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
        public Rounds Rounds { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Rounds = GetComponent<Rounds>();
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

        /// <summary>
        /// Determines if missiles should fire based on the given targeting context.
        /// Used by AI to make firing decisions.
        /// </summary>
        public override bool ShouldFire(TargetingContext context)
        {
            if (!Rounds || Rounds.AmmoCount <= 0) return false;

            switch (targetingComputer.State)
            {
                case LockState.Locked:
                    return true;
                case LockState.Idle:
                case LockState.Locking:
                    const float missileRange = 10f;
                    const float missileAngleTolerance = 15f;
                    return context is { DistanceToTarget: <= missileRange, AngleToTarget: <= missileAngleTolerance };
                case LockState.Cooldown:
                default:
                    return false;
            }
        }
    }
} 
