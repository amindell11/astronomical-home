using Balance;
using Combat.Projectiles;
using UnityEngine;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;

namespace Combat.Weapons.Arsenal
{
    public class Lasers : WeaponBase<Laser>
    {
        [Tooltip("Fire the hull's left and right sockets together on each shot instead of firing from the centre.")]
        [SerializeField] private bool dualFire;

        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will open fire.")]
        [Stat, SerializeField, Min(0f)] private float fireDistance = 20f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will open fire.")]
        [Stat, SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 5f;
        [Tooltip("Once the next shot would overheat, an AI gunner holds fire until heat falls to this fraction of max. 0 = vent to cold; 1 = fire again as soon as the next shot fits.")]
        [Stat, SerializeField, Range(0f, 1f)] private float ventTo = 1f;

        [Header("Bank Shot")]
        [SerializeField] private WeaponBehaviourMod bankShot;
        [Tooltip("With Bank shot held: multiplier on a bolt's damage once it has banked off a rock.")]
        [Stat, SerializeField, Min(0f)] private float bankedDamageScale = 2f;

        [Header("Point Defence")]
        [Tooltip("Without it held, bolts fly through enemy missiles and charges.")]
        [SerializeField] private WeaponBehaviourMod pointDefence;

        [Header("Conditions")]
        [Stat, SerializeField] private Heat heat;
        [Stat, SerializeField] private Cooldown cooldown;

        private bool venting;

        public override FirePattern FirePattern => dualFire ? FirePattern.Volley : FirePattern.Single;
        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public override float FireRange => fireDistance;
        public Heat Heat => heat;
        protected override WeaponCondition[] AuthoredConditions => new WeaponCondition[] { heat, cooldown };
        public float Damage => projectilePrefab.Damage;
        public float? ShotsPerSecond =>
            cooldown && cooldown.SecondsBetweenShots > 0f ? 1f / cooldown.SecondsBetweenShots : (float?)null;
        public int? ShotsToOverheat =>
            heat ? heat.ShotsToOverheat(cooldown ? cooldown.SecondsBetweenShots : 0f) : null;

        protected override void Awake()
        {
            base.Awake();
            if (!heat) heat = GetComponent<Heat>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        protected override void Prime(Laser bolt)
        {
            if (Behaviours.Holds(bankShot)) bolt.BankOnce(bankedDamageScale);
            if (Behaviours.Holds(pointDefence)) bolt.Intercept();
        }

        public override bool InEnvelope(in TargetingContext context) =>
            context.Within(fireDistance, fireAngleTolerance);

        public override bool HoldTrigger(TargetingContext context)
        {
            if (!Heat) return false;
            venting = Heat.WouldOverheatOnNextShot() || (venting && Heat.HeatPct > ventTo);
            return !venting && InEnvelope(in context);
        }

        public override void Reset()
        {
            base.Reset();
            venting = false;
        }
    }
}
