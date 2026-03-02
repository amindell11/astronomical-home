using Combat.Conditions;
using Combat.Projectile;
using Combat.Targeting;
using UnityEngine;
using Missile = Combat.Projectile.Missile;

namespace Combat.Weapons
{
    public partial class WeaponMissiles : WeaponBase<Missile>
    {
        private const float DefaultMissileRange = 10f;
        private const float DefaultMissileAngleTolerance = 15f;

        [Header("Targeting")]
        [SerializeField] private TargetingComputer targetingComputer;

        [Header("AI")]
        [SerializeField] private CombatTuning tuning;

        private ILockProvider lockProvider;

        public TargetingComputer Targeting => targetingComputer;
        public Rounds Rounds { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Rounds = GetComponent<Rounds>();
            if (!targetingComputer)
                targetingComputer = GetComponent<TargetingComputer>();
            lockProvider = targetingComputer;
        }

        public void SetLockProvider(ILockProvider provider)
        {
            lockProvider = provider;
        }

        public override ProjectileBase Fire()
        {
            var proj = base.Fire() as Missile;
            if (!proj)
                return null;

            var lockedTarget = lockProvider?.ConsumeLock();
            if (lockedTarget != null)
                proj.SetTarget(lockedTarget.TargetPoint);

            return proj;
        }

        public override bool ShouldFire(TargetingContext context)
        {
            if (!Rounds || Rounds.AmmoCount <= 0)
                return false;

            switch (lockProvider?.State ?? LockState.Idle)
            {
                case LockState.Locked:
                    return true;
                case LockState.Idle:
                case LockState.Locking:
                    return context.distanceToTarget <= MissileRange && context.angleToTarget <= MissileAngleTolerance;
                case LockState.Cooldown:
                default:
                    return false;
            }
        }

        private float MissileRange => tuning ? tuning.MissileFallbackRange : DefaultMissileRange;
        private float MissileAngleTolerance => tuning ? tuning.MissileFallbackAngleTolerance : DefaultMissileAngleTolerance;
    }
}
