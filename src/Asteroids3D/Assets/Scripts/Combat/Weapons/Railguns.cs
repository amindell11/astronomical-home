using Combat.Projectiles;
using System;
using Damage;
using Game.Services;
using UnityEngine;
using Utils;
using Game.Services.Projectiles;
using Combat.Weapons.Conditions;

namespace Combat.Weapons
{
    /// <summary>Charged hitscan railgun (<see cref="ProjectileSpeed"/> 0 — AI gunners aim with no intercept lead).</summary>
    public class Railguns : WeaponComponent
    {
        [Header("Beam")]
        [Tooltip("Damage applied to the first thing the beam hits.")]
        [SerializeField, Min(0f)] private float damage = 45f;
        [Tooltip("Max beam length.")]
        [SerializeField, Min(0f)] private float range = 60f;
        [Tooltip("Nominal impact speed reported to damage handling (knockback/VFX plausibility).")]
        [SerializeField, Min(0f)] private float impactSpeed = 120f;
        [Tooltip("Mass reported to damage handling.")]
        [SerializeField, Min(0f)] private float impactMass = 0.05f;
        [Tooltip("Layers the beam can hit. -1 = Ship | Asteroid.")]
        [SerializeField] private LayerMask hitMask = -1;

        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will hold the charge trigger.")]
        [SerializeField, Min(0f)] private float fireDistance = 45f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will hold the charge trigger.")]
        [SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 2f;

        /// <summary>Raised per shot with the beam's world start and end points (for visuals).</summary>
        public event Action<Vector3, Vector3> OnBeamFired;

        [Header("Conditions")]
        [SerializeField] private ChargeTime charge;

        public ChargeTime Charge => charge;

        public override float ProjectileSpeed => 0f;

        public override float FireRange => fireDistance;

        public override string HangarStats
        {
            get
            {
                var chargeText = charge ? $"   |   Full charge {charge.FullChargeTime:0.#}s" : "";
                return $"Damage {damage:0}   |   Range {range:0}{chargeText}   |   Hitscan";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (!charge) charge = GetComponent<ChargeTime>();
            if (hitMask == -1)
                hitMask = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid);
        }

        public override void HandleTrigger(bool pressed, bool held, IProjectileService projectiles)
        {
            if (Charge && Charge.HandleTrigger(held, Time.fixedDeltaTime))
                Fire(projectiles);
        }

        // Hitscan: the registry is part of the uniform firing surface but there is never a projectile to track.
        public override ProjectileBase Fire(IProjectileService projectiles)
        {
            if (!CanFire()) return null;

            foreach (var condition in conditions)
                condition.ProcessFire();

            FireBeam();
            InvokeOnFire();

            // Hitscan: there is no projectile to hand back.
            return null;
        }

        public override bool InEnvelope(in TargetingContext context) =>
            context.hasLineOfSight
            && context.distanceToTarget <= fireDistance
            && context.angleToTarget <= fireAngleTolerance;

        // The AI holds the trigger to charge; ChargeTime auto-fires at full, so readiness adds nothing here.
        public override bool ShouldFire(TargetingContext context) => InEnvelope(in context);

        private static readonly RaycastHit[] beamHits = new RaycastHit[16];

        private void FireBeam()
        {
            var origin = firePoint.position;
            var direction = firePoint.up;
            var end = origin + direction * range;

            // The beam starts inside the firing ship, so skip the shooter's own body.
            var shooterBody = shooter?.Body;
            var count = Physics.RaycastNonAlloc(origin, direction, beamHits, range, hitMask);
            var bestIndex = -1;
            for (var i = 0; i < count; i++)
            {
                if (shooterBody && beamHits[i].rigidbody == shooterBody) continue;
                if (bestIndex < 0 || beamHits[i].distance < beamHits[bestIndex].distance)
                    bestIndex = i;
            }

            if (bestIndex >= 0)
            {
                end = beamHits[bestIndex].point;
                ApplyBeamDamage(beamHits[bestIndex]);
            }

            OnBeamFired?.Invoke(origin, end);
        }

        private void ApplyBeamDamage(in RaycastHit hit)
        {
            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null) return;

            damageable.TakeDamage(new DamageInfo(damage, DamageKind.Railgun, shooter?.Id ?? Ships.Registry.ShipId.Invalid,
                impactMass, firePoint.up * impactSpeed, hit.point));
        }
    }
}
