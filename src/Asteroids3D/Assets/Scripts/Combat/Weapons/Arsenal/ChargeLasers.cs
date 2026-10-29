using Balance;
using Combat.Projectiles;
using UnityEngine;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;

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
        [Tooltip("Max distance at which an AI gunner charges and releases.")]
        [Stat, SerializeField, Min(0f)] private float fireDistance = 25f;
        [Tooltip("Max aim error (degrees) at which an AI gunner releases a charged shot.")]
        [Stat, SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 5f;
        [Tooltip("Max aim error (degrees) at which an AI gunner starts a charge. Once started, it keeps charging while in range.")]
        [Stat, SerializeField, Range(0f, 180f)] private float chargeCone = 25f;
        [Tooltip("Charge fraction from which an AI gunner releases on the first step the shot lines up and the weapon is ready. It never releases below the charge's minimum to fire.")]
        [Stat, SerializeField, Range(0f, 1f)] private float releaseCharge = 0.31f;
        [Tooltip("Max aim error (degrees) at which an AI gunner releases a charge about to reach full, rather than let it fire blind at full.")]
        [Stat, SerializeField, Range(0f, 180f)] private float fullChargeReleaseCone = 12f;

        [Header("Conditions")]
        [Stat, SerializeField] private ChargeTime charge;
        [Stat, SerializeField] private Cooldown cooldown;

        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public override float FireRange => fireDistance;
        public ChargeTime Charge => charge;
        protected override WeaponCondition[] AuthoredConditions => new WeaponCondition[] { charge, cooldown };
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
            var proj = base.Fire(targetPoint, projectiles);
            if (proj == null) return null;

            var spent = Charge ? Charge.SpentCharge : 1f;
            proj.SetDamageScale(Mods.Factor(WeaponModField.Damage) *
                                Mathf.Lerp(minChargeDamageScale, fullChargeDamageScale, spent));
            return proj;
        }

        public override bool InEnvelope(in TargetingContext context) =>
            context.Within(fireDistance, fireAngleTolerance);

        public override bool HoldTrigger(TargetingContext context)
        {
            if (charge.ChargePct < releaseCharge)
                return context.distanceToTarget <= fireDistance
                    && (charge.ChargePct > 0f || context.angleToTarget <= chargeCone);

            // Not ready (cooldown, or under the charge's minimum): a release now fires nothing.
            if (!CanFire()) return true;
            if (InEnvelope(in context)) return false;
            return !(charge.FullAfterHeldStep(Time.fixedDeltaTime) && context.angleToTarget <= fullChargeReleaseCone);
        }
    }
}
