using Combat.Conditions;
using Combat.Projectile;
using Combat.Targeting;
using UnityEngine;
using Missile = Combat.Projectile.Missile;

namespace Combat.Weapons
{
    public partial class Missiles : WeaponBase<Missile>
    {
        [Header("Targeting")]
        [SerializeField] private LockOnSensor targetingComputer;

        [Header("AI Firing (No Lock)")]
        [Tooltip("Max distance at which an AI gunner will fire unguided (no lock).")]
        [SerializeField, Min(0f)] private float fallbackRange = 10f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will fire unguided (no lock).")]
        [SerializeField, Range(0f, 180f)] private float fallbackAngleTolerance = 15f;

        private ILockProvider lockProvider;

        public LockOnSensor Targeting => targetingComputer;
        public Rounds Rounds { get; private set; }

        public override ILockStateSource LockSource => targetingComputer;

        // Missiles are semi-auto: one launch per trigger press, not a held stream.
        public override bool AutoFire => false;

        protected override void Awake()
        {
            base.Awake();
            Rounds = GetComponent<Rounds>();
            if (!targetingComputer)
                targetingComputer = GetComponent<LockOnSensor>();
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
                    return context.distanceToTarget <= fallbackRange && context.angleToTarget <= fallbackAngleTolerance;
                case LockState.Cooldown:
                default:
                    return false;
            }
        }
    }
}
