using Combat.Conditions;
using Combat.Projectile;

namespace Combat.Weapons
{
    public partial class WeaponLaser : WeaponBase<LaserProjectile>
    {
        public float ProjectileSpeed => projectilePrefab.LaserSpeed;

        private Heat heat;

        protected override void Awake()
        {
            base.Awake();
            heat = GetComponent<Heat>();
        }

        /// <summary>
        /// Determines if the laser should fire based on the given targeting context.
        /// Used by AI to make firing decisions.
        /// </summary>
        public override bool ShouldFire(TargetingContext context)
        {
            if (!heat || !context.HasLineOfSight) return false;

            var isReadyToFire = !heat.WouldOverheatOnNextShot();
            if (!isReadyToFire) return false;
            
            const float fireDistance = 20f;
            const float fireAngleTolerance = 5f;
            
            var isInRange = context.DistanceToTarget <= fireDistance;
            var isInAngle = context.AngleToTarget <= fireAngleTolerance;

            return isInRange && isInAngle;
        }
    }
} 