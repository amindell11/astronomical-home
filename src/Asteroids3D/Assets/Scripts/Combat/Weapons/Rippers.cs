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

        [Header("Conditions")]
        [SerializeField] private Rounds rounds;
        [SerializeField] private Cooldown cooldown;

        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public Rounds Rounds => rounds;

        public override string HangarStats
        {
            get
            {
                if (!projectilePrefab) return DisplayName;
                var rate = cooldown && cooldown.SecondsBetweenShots > 0f
                    ? $"   |   Rate {1f / cooldown.SecondsBetweenShots:0.#}/s" : "";
                var mag = rounds
                    ? $"   |   Mag {rounds.MaxAmmo}" + (rounds.ReloadTime > 0f ? $" (reload {rounds.ReloadTime:0.#}s)" : "")
                    : "";
                return $"Damage {projectilePrefab.Damage:0}{rate}{mag}   |   Speed {projectilePrefab.LaserSpeed:0}";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
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
