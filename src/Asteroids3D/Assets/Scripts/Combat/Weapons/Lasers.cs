using System.Collections.Generic;
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
            heat && cooldown ? heat.ShotsToOverheat(cooldown.SecondsBetweenShots) : null;

        public override IReadOnlyList<WeaponCycleMode> CycleModes
        {
            get
            {
                var interval = cooldown.SecondsBetweenShots;
                if (heat.ShotsToOverheat(interval) is not int shots)
                    return new[] { new WeaponCycleMode("sustained", Damage, interval, 0f) };

                var overheat = new WeaponCycleMode("overheat", shots * Damage, (shots - 1) * interval,
                    heat.OverheatRecoverySeconds);
                // AI gunners stop one shot short of overheating; a one-shot gauge leaves them nothing to fire.
                var managedShots = shots - 1;
                if (managedShots < 1) return new[] { overheat };

                var managed = new WeaponCycleMode("managed", managedShots * Damage, (managedShots - 1) * interval,
                    heat.BurstRecoverySeconds(managedShots, interval));
                return new[] { overheat, managed };
            }
        }

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
