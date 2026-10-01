using Combat.Projectiles;
using UnityEngine;
using Combat.Weapons.Conditions;

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
        public float Damage => projectilePrefab.Damage;
        public float? ShotsPerSecond =>
            cooldown && cooldown.SecondsBetweenShots > 0f ? 1f / cooldown.SecondsBetweenShots : (float?)null;

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
