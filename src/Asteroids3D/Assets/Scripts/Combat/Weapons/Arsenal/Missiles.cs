using Balance;
using Combat.Projectiles;
using Combat.Targeting;
using UnityEngine;
using Missile = Combat.Projectiles.Missile;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;

namespace Combat.Weapons.Arsenal
{
    public class Missiles : WeaponBase<Missile>
    {
        [Header("Targeting")]
        [Stat, SerializeField] private LockOnSensor targetingComputer;

        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        [Header("AI Firing")]
        [Tooltip("Distance an AI pilot carrying this weapon holds from its target.")]
        [Stat, SerializeField, Min(0f)] private float holdRange = 13f;

        [Header("AI Firing (No Lock)")]
        [Tooltip("Max distance at which an AI gunner will fire unguided (no lock). Keep it inside the distance the scripted pilots hold from their target: a pilot hovering on this edge launches unguided.")]
        [Stat, SerializeField, Min(0f)] private float fallbackRange = 7f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will fire unguided (no lock).")]
        [Stat, SerializeField, Range(0f, 180f)] private float fallbackAngleTolerance = 15f;

        public Rounds Rounds => rounds;
        public float Damage => projectilePrefab.Damage;
        public float SplashDamage => projectilePrefab.SplashDamage;

        public override ILockStateSource LockSource => targetingComputer;

        // Missiles are semi-auto: one launch per trigger press, not a held stream.
        public override bool AutoFire => false;
        public override float? HoldRange => holdRange;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
            if (!targetingComputer)
                targetingComputer = GetComponent<LockOnSensor>();
        }

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            var proj = base.Fire(targetPoint, projectiles) as Missile;
            if (!proj)
                return null;

            if (targetingComputer)
            {
                var lockedTarget = targetingComputer.ConsumeLock();
                if (lockedTarget != null)
                    proj.SetTarget(lockedTarget);
            }

            return proj;
        }

        // The lock cone, or the dumbfire window where an AI gunner launches without a lock.
        public override bool InEnvelope(in TargetingContext context) =>
            InLockCone(in context) || InFallbackEnvelope(in context);

        private bool InLockCone(in TargetingContext context) =>
            targetingComputer
            && context.Within(targetingComputer.maxLockDistance, targetingComputer.lockOnConeAngle * 0.5f);

        private bool InFallbackEnvelope(in TargetingContext context) =>
            context.Within(fallbackRange, fallbackAngleTolerance);

        // Not a strict InEnvelope && readiness factorization: a held lock fires regardless of current geometry.
        public override bool HoldTrigger(TargetingContext context)
        {
            // Readiness drops the solution after each launch, so the gunner's press edge re-arms.
            if (!CanFire())
                return false;

            var lockState = LockState.Idle;
            if (targetingComputer)
                lockState = targetingComputer.State;

            return lockState switch
            {
                LockState.Locked => true,
                LockState.Idle or LockState.Locking => InFallbackEnvelope(in context),
                _ => false,
            };
        }
    }
}
