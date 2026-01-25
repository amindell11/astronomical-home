using Combat.Conditions;
using Combat.Projectile;

namespace Combat.Weapons
{
    public partial class WeaponLaser : WeaponBase<LaserProjectile>
    {
        public float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public Heat Heat { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Heat = GetComponent<Heat>();
        }

        /// <summary>
        /// Determines if the laser should fire based on the given targeting context.
        /// Used by AI to make firing decisions.
        /// </summary>
        public override bool ShouldFire(TargetingContext context)
        {
            if (!Heat || !context.HasLineOfSight) return false;

            var isReadyToFire = !Heat.WouldOverheatOnNextShot();
            if (!isReadyToFire) return false;
            
            const float fireDistance = 20f;
            const float fireAngleTolerance = 5f;
            
            var isInRange = context.DistanceToTarget <= fireDistance;
            var isInAngle = context.AngleToTarget <= fireAngleTolerance;

            return isInRange && isInAngle;
        }
    }
} 
