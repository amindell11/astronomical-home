using Balance;
using Combat.Projectiles;
using UnityEngine;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;

namespace Combat.Weapons.Arsenal
{
    /// <summary>Hold-to-charge laser: damage scales with the <see cref="ChargeTime"/> charge spent on release.</summary>
    public class ChargeLasers : WeaponBase<Laser>
    {
        [Header("Charge Damage")]
        [Tooltip("Damage multiplier at minimum charge; scales linearly to the full-charge multiplier.")]
        [Stat, SerializeField, Min(0f)] private float minChargeDamageScale = 0.4f;
        [Tooltip("Damage multiplier at full charge.")]
        [Stat, SerializeField, Min(0f)] private float fullChargeDamageScale = 1f;

        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will hold the charge trigger.")]
        [Stat, SerializeField, Min(0f)] private float fireDistance = 25f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will hold the charge trigger.")]
        [Stat, SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 5f;

        [Header("Conditions")]
        [Stat, SerializeField] private ChargeTime charge;
        [Stat, SerializeField] private Cooldown cooldown;

        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public override float FireRange => fireDistance;
        public ChargeTime Charge => charge;
        public float MinChargeDamage => projectilePrefab.Damage * minChargeDamageScale;
        public float FullChargeDamage => projectilePrefab.Damage * fullChargeDamageScale;

        protected override void Awake()
        {
            base.Awake();
            if (!charge) charge = GetComponent<ChargeTime>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        public override void HandleTrigger(in WeaponCommand cmd, IProjectileService projectiles)
        {
            if (Charge && Charge.HandleTrigger(cmd.held, Time.fixedDeltaTime))
                Fire(cmd.targetPoint, projectiles);
        }

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            // Captured before firing: ProcessFire consumes the charge.
            var charge = Charge ? Charge.ChargePct : 1f;

            var proj = base.Fire(targetPoint, projectiles);
            if (proj != null)
                proj.SetDamageScale(Mathf.Lerp(minChargeDamageScale, fullChargeDamageScale, charge));
            return proj;
        }

        public override bool InEnvelope(in TargetingContext context) =>
            context.hasLineOfSight
            && context.distanceToTarget <= fireDistance
            && context.angleToTarget <= fireAngleTolerance;

        // The AI holds the trigger to charge; ChargeTime auto-fires at full, so readiness adds nothing here.
        public override bool ShouldFire(TargetingContext context) => InEnvelope(in context);
    }
}
