using Combat.Conditions;
using Combat.Projectile;
using UnityEngine;

namespace Combat.Weapons
{
    public partial class Lasers : WeaponBase<Laser>
    {
        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will open fire.")]
        [SerializeField, Min(0f)] private float fireDistance = 20f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will open fire.")]
        [SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 5f;

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

            var isInRange = context.distanceToTarget <= fireDistance;
            var isInAngle = context.angleToTarget <= fireAngleTolerance;

            return isInRange && isInAngle;
        }
    }
}
