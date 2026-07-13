using Combat.Conditions;
using Combat.Projectile;
using UnityEngine;

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

        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public ChargeTime Charge => charge;

        public override string HangarStats
        {
            get
            {
                if (!projectilePrefab) return DisplayName;
                var chargeText = charge ? $"   |   Full charge {charge.FullChargeTime:0.#}s" : "";
                var damage = projectilePrefab.Damage;
                return $"Damage {damage * minChargeDamageScale:0}-{damage * fullChargeDamageScale:0}{chargeText}" +
                       $"   |   Speed {projectilePrefab.LaserSpeed:0}";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (!charge) charge = GetComponent<ChargeTime>();
        }

        public override void HandleTrigger(bool pressed, bool held)
        {
            if (Charge && Charge.HandleTrigger(held, Time.fixedDeltaTime))
                Fire();
        }

        public override ProjectileBase Fire()
        {
            // Captured before firing: ProcessFire consumes the charge.
            var charge = Charge ? Charge.ChargePct : 1f;

            var proj = base.Fire();
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
