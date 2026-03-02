using Combat.Conditions;
using Combat.Projectile;
using UnityEngine;

namespace Combat.Weapons
{
    public partial class WeaponLaser : WeaponBase<LaserProjectile>
    {
        private const float DefaultFireDistance = 20f;
        private const float DefaultFireAngleTolerance = 5f;

        [SerializeField] private CombatTuning tuning;

        public float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public Heat Heat { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Heat = GetComponent<Heat>();
        }

        public override bool ShouldFire(TargetingContext context)
        {
            if (!Heat || !context.hasLineOfSight)
                return false;

            if (Heat.WouldOverheatOnNextShot())
                return false;

            var isInRange = context.distanceToTarget <= FireDistance;
            var isInAngle = context.angleToTarget <= FireAngleTolerance;

            return isInRange && isInAngle;
        }

        private float FireDistance => tuning ? tuning.LaserFireDistance : DefaultFireDistance;
        private float FireAngleTolerance => tuning ? tuning.LaserFireAngleTolerance : DefaultFireAngleTolerance;
    }
}
