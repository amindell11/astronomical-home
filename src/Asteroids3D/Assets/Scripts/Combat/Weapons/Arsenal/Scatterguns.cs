using System;
using Balance;
using Combat.Projectiles;
using UnityEngine;
using Combat.Weapons.Conditions;
using Substrate.Services.Projectiles;

namespace Combat.Weapons.Arsenal
{
    /// <summary>Short-range spread gun: one trigger pull fans its pellets across a cone and spends one shell; each pellet knocks back what it hits.</summary>
    public class Scatterguns : WeaponBase<Pellet>
    {
        [Header("Spread")]
        [Tooltip("Pellets per trigger pull.")]
        [Stat, SerializeField, Min(1)] private int pellets = 8;
        [Tooltip("Full width of the fan.")]
        [Stat, SerializeField, Range(0f, 90f)] private float spreadDegrees = 20f;

        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will open fire.")]
        [Stat, SerializeField, Min(0f)] private float fireDistance = 12f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will open fire.")]
        [Stat, SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 10f;
        [Tooltip("Distance an AI pilot carrying this weapon holds from its target.")]
        [Stat, SerializeField, Min(0f)] private float holdRange = 8f;

        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        public override FirePattern FirePattern => FirePattern.Single;
        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public override float FireRange => fireDistance;
        public override float? HoldRange => holdRange;
        public Rounds Rounds => rounds;
        protected override WeaponCondition[] AuthoredConditions => new WeaponCondition[] { rounds, cooldown };
        public float Damage => projectilePrefab.Damage;
        public float Range => projectilePrefab.MaxDistance;
        public float Knockback => projectilePrefab.Knockback;
        public int Pellets => pellets;
        public float SpreadDegrees => spreadDegrees;
        public float? ShotsPerSecond =>
            cooldown && cooldown.SecondsBetweenShots > 0f ? 1f / cooldown.SecondsBetweenShots : (float?)null;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            if (projectiles == null) throw new ArgumentNullException(nameof(projectiles));
            if (!TrySpendFireConditions()) return null;

            var muzzle = Muzzles[0];
            Pellet first = null;
            var half = spreadDegrees * 0.5f;
            var last = pellets - 1;
            for (var i = 0; i < pellets; i++)
            {
                var yaw = last <= 0 ? 0f : Mathf.Lerp(-half, half, i / (float)last);
                var pellet = Launch(muzzle, targetPoint, projectiles, yaw);
                if (!first) first = pellet;
            }

            InvokeOnFire();
            return first;
        }

        public override bool InEnvelope(in TargetingContext context) =>
            context.Within(fireDistance, fireAngleTolerance);

        public override bool HoldTrigger(TargetingContext context) =>
            Rounds && Rounds.CanFire() && InEnvelope(in context);
    }
}
