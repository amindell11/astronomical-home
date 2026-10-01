using Combat.Projectiles;
using Combat.Targeting;
using UnityEngine;
using Missile = Combat.Projectiles.Missile;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;

namespace Combat.Weapons
{
    public class Missiles : WeaponBase<Missile>
    {
        [Header("Targeting")]
        [SerializeField] private LockOnSensor targetingComputer;

        [Header("Conditions")]
        [SerializeField] private Rounds rounds;

        [Header("AI Firing (No Lock)")]
        [Tooltip("Max distance at which an AI gunner will fire unguided (no lock). Keep it inside the distance the scripted pilots hold from their target: a pilot hovering on this edge launches unguided.")]
        [SerializeField, Min(0f)] private float fallbackRange = 7f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will fire unguided (no lock).")]
        [SerializeField, Range(0f, 180f)] private float fallbackAngleTolerance = 15f;

        private ILockProvider lockProvider;

        public LockOnSensor Targeting => targetingComputer;
        public Rounds Rounds => rounds;
        public float Damage => projectilePrefab.Damage;
        public float SplashDamage => projectilePrefab.SplashDamage;

        public override ILockStateSource LockSource => targetingComputer;

        // Missiles are semi-auto: one launch per trigger press, not a held stream.
        public override bool AutoFire => false;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!targetingComputer)
                targetingComputer = GetComponent<LockOnSensor>();
            lockProvider = targetingComputer;
        }

        public override ProjectileBase Fire(IProjectileService projectiles)
        {
            var proj = base.Fire(projectiles) as Missile;
            if (!proj)
                return null;

            var lockedTarget = lockProvider?.ConsumeLock();
            if (lockedTarget != null)
                proj.SetTarget(lockedTarget.TargetPoint);

            return proj;
        }

        // The lock cone, or the dumbfire window where an AI gunner launches without a lock.
        public override bool InEnvelope(in TargetingContext context) =>
            InLockCone(in context) || InFallbackEnvelope(in context);

        private bool InLockCone(in TargetingContext context) =>
            targetingComputer
            && context.hasLineOfSight
            && context.distanceToTarget <= targetingComputer.maxLockDistance
            && context.angleToTarget <= targetingComputer.lockOnConeAngle * 0.5f;

        private bool InFallbackEnvelope(in TargetingContext context) =>
            context.hasLineOfSight
            && context.distanceToTarget <= fallbackRange
            && context.angleToTarget <= fallbackAngleTolerance;

        // Not a strict InEnvelope && readiness factorization: a held lock fires regardless of current geometry.
        public override bool ShouldFire(TargetingContext context)
        {
            // Readiness drops the solution after each launch, so the gunner's press edge re-arms.
            if (!CanFire())
                return false;

            return (lockProvider?.State ?? LockState.Idle) switch
            {
                LockState.Locked => true,
                LockState.Idle or LockState.Locking => InFallbackEnvelope(in context),
                _ => false,
            };
        }
    }
}
