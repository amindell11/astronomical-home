using System.Collections.Generic;
using Combat.Projectiles;
using UnityEngine;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;

namespace Combat.Weapons
{
    /// <summary>Hold-to-charge laser: damage scales with the <see cref="ChargeTime"/> charge spent on release.</summary>
    public class ChargeLasers : WeaponBase<Laser>
    {
        [Header("Charge Damage")]
        [Tooltip("Damage multiplier at minimum charge; scales linearly to the full-charge multiplier.")]
        [SerializeField, Min(0f)] private float minChargeDamageScale = 0.4f;
        [Tooltip("Damage multiplier at full charge.")]
        [SerializeField, Min(0f)] private float fullChargeDamageScale = 1f;

        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will hold the charge trigger.")]
        [SerializeField, Min(0f)] private float fireDistance = 25f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will hold the charge trigger.")]
        [SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 5f;

        [Header("Conditions")]
        [SerializeField] private ChargeTime charge;
        [SerializeField] private Cooldown cooldown;

        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public override float FireRange => fireDistance;
        public ChargeTime Charge => charge;
        public float MinChargeDamage => projectilePrefab.Damage * minChargeDamageScale;
        public float FullChargeDamage => projectilePrefab.Damage * fullChargeDamageScale;

        public override IReadOnlyList<WeaponCycleMode> CycleModes
        {
            get
            {
                var interval = cooldown.SecondsBetweenShots;
                var fullCharge = charge.FullChargeTime;
                // Charge accrues while the cooldown runs, so a tap waits for whichever is longer.
                var tap = Mathf.Max(charge.MinChargeTime, interval);
                return new[]
                {
                    new WeaponCycleMode("full charge", FullChargeDamage, fullCharge, Mathf.Max(0f, interval - fullCharge)),
                    new WeaponCycleMode("min charge", projectilePrefab.Damage * DamageScaleAt(tap / fullCharge), tap, 0f),
                };
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (!charge) charge = GetComponent<ChargeTime>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        public override void HandleTrigger(bool pressed, bool held, IProjectileService projectiles)
        {
            if (Charge && Charge.HandleTrigger(held, Time.fixedDeltaTime))
                Fire(projectiles);
        }

        public override ProjectileBase Fire(IProjectileService projectiles)
        {
            // Captured before firing: ProcessFire consumes the charge.
            var charge = Charge ? Charge.ChargePct : 1f;

            var proj = base.Fire(projectiles);
            if (proj != null)
                proj.SetDamageScale(DamageScaleAt(charge));
            return proj;
        }

        private float DamageScaleAt(float chargePct) =>
            Mathf.Lerp(minChargeDamageScale, fullChargeDamageScale, chargePct);

        public override bool InEnvelope(in TargetingContext context) =>
            context.hasLineOfSight
            && context.distanceToTarget <= fireDistance
            && context.angleToTarget <= fireAngleTolerance;

        // The AI holds the trigger to charge; ChargeTime auto-fires at full, so readiness adds nothing here.
        public override bool ShouldFire(TargetingContext context) => InEnvelope(in context);
    }
}
