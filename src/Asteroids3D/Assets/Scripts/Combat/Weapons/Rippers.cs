using Combat.Conditions;
using Combat.Projectile;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>Ballistic autocannon: magazine-fed (<see cref="Rounds"/>), no heat, laser-class straight-line slugs.</summary>
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
        public override float FireRange => fireDistance;
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

        public override bool InEnvelope(in TargetingContext context) =>
            context.hasLineOfSight
            && context.distanceToTarget <= fireDistance
            && context.angleToTarget <= fireAngleTolerance;

        public override bool ShouldFire(TargetingContext context) =>
            Rounds && Rounds.CanFire() && InEnvelope(in context);
    }
}
