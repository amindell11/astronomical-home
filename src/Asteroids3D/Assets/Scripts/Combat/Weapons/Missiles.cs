using Combat.Projectiles;
using Combat.Targeting;
using Game.Services;
using UnityEngine;
using Missile = Combat.Projectiles.Missile;
using Game.Services.Projectiles;
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
        [Tooltip("Max distance at which an AI gunner will fire unguided (no lock).")]
        [SerializeField, Min(0f)] private float fallbackRange = 10f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will fire unguided (no lock).")]
        [SerializeField, Range(0f, 180f)] private float fallbackAngleTolerance = 15f;

        private ILockProvider lockProvider;

        public LockOnSensor Targeting => targetingComputer;
        public Rounds Rounds => rounds;

        public override ILockStateSource LockSource => targetingComputer;

        // Missiles are semi-auto: one launch per trigger press, not a held stream.
        public override bool AutoFire => false;

        public override string HangarStats
        {
            get
            {
                if (!projectilePrefab) return DisplayName;
                var ammo = rounds
                    ? $"   |   {rounds.MaxAmmo} rounds" + (rounds.ReloadTime > 0f ? $" (reload {rounds.ReloadTime:0.#}s)" : "")
                    : "";
                return $"Damage {projectilePrefab.Damage:0} + {projectilePrefab.SplashDamage:0} splash{ammo}   |   Lock-on homing";
            }
        }

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

        // Honest geometry: the sensor's lock cone (with LOS) or the unguided dumbfire window (no LOS, matching ShouldFire's fallback).
        public override bool InEnvelope(in TargetingContext context) =>
            InLockCone(in context) || InFallbackEnvelope(in context);

        private bool InLockCone(in TargetingContext context) =>
            targetingComputer
            && context.hasLineOfSight
            && context.distanceToTarget <= targetingComputer.maxLockDistance
            && context.angleToTarget <= targetingComputer.lockOnConeAngle * 0.5f;

        private bool InFallbackEnvelope(in TargetingContext context) =>
            context.distanceToTarget <= fallbackRange && context.angleToTarget <= fallbackAngleTolerance;

        // Not a strict InEnvelope && readiness factorization: a held lock fires regardless of current geometry.
        public override bool ShouldFire(TargetingContext context)
        {
            if (!Rounds || Rounds.AmmoCount <= 0)
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
