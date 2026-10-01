using Combat.Projectiles;
using UnityEngine;
using Combat.Weapons.Conditions;

namespace Combat.Weapons
{
    public class Lasers : WeaponBase<Laser>
    {
        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will open fire.")]
        [SerializeField, Min(0f)] private float fireDistance = 20f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will open fire.")]
        [SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 5f;

        [Header("Conditions")]
        [SerializeField] private Heat heat;
        [SerializeField] private Cooldown cooldown;

        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public override float FireRange => fireDistance;
        public Heat Heat => heat;
        public float Damage => projectilePrefab.Damage;
        public float? ShotsPerSecond =>
            cooldown && cooldown.SecondsBetweenShots > 0f ? 1f / cooldown.SecondsBetweenShots : (float?)null;
        public int? ShotsToOverheat =>
            heat && heat.HeatPerShot > 0f ? Mathf.FloorToInt(heat.MaxHeat / heat.HeatPerShot) : (int?)null;

        protected override void Awake()
        {
            base.Awake();
            if (!heat) heat = GetComponent<Heat>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        public override bool InEnvelope(in TargetingContext context) =>
            context.hasLineOfSight
            && context.distanceToTarget <= fireDistance
            && context.angleToTarget <= fireAngleTolerance;

        public override bool ShouldFire(TargetingContext context) =>
            Heat && !Heat.WouldOverheatOnNextShot() && InEnvelope(in context);
    }
}
