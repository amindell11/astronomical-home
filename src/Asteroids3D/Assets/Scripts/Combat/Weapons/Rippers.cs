using Combat.Conditions;
using Combat.Projectile;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>
    /// Ballistic autocannon: magazine-fed (<see cref="Rounds"/> with a timed reload), no heat.
    /// Fires the same straight-line projectile class as the laser from a slug-tuned prefab.
    /// </summary>
    public class Rippers : WeaponBase<Laser>
    {
        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will open fire.")]
        [SerializeField, Min(0f)] private float fireDistance = 18f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will open fire.")]
        [SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 6f;

        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public Rounds Rounds { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Rounds = GetComponent<Rounds>();
        }

        public override bool ShouldFire(TargetingContext context)
        {
            if (!Rounds || !Rounds.CanFire() || !context.hasLineOfSight)
                return false;

            var isInRange = context.distanceToTarget <= fireDistance;
            var isInAngle = context.angleToTarget <= fireAngleTolerance;

            return isInRange && isInAngle;
        }
    }
}
