using System;
using Combat.Conditions;
using Combat.Projectile;
using Damage;
using UnityEngine;
using Utils;

namespace Combat.Weapons
{
    /// <summary>
    /// Charged railgun: hold to charge (full charge required — see the prefab's
    /// <see cref="ChargeTime"/>), then the shot lands instantly along the fire point's aim as a
    /// raycast. Hitscan, so <see cref="ProjectileSpeed"/> is 0: AI gunners aim at the target's
    /// present position with no intercept lead.
    /// </summary>
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

        public ChargeTime Charge { get; private set; }

        // Hitscan: no muzzle speed, no intercept lead.
        public override float ProjectileSpeed => 0f;

        public override string HangarStats
        {
            get
            {
                var charge = GetComponent<ChargeTime>();
                var chargeText = charge ? $"   |   Full charge {charge.FullChargeTime:0.#}s" : "";
                return $"Damage {damage:0}   |   Range {range:0}{chargeText}   |   Hitscan";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Charge = GetComponent<ChargeTime>();
            if (hitMask == -1)
                hitMask = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid);
        }

        public override void HandleTrigger(bool pressed, bool held)
        {
            if (Charge && Charge.HandleTrigger(held, Time.fixedDeltaTime))
                Fire();
        }

        public override ProjectileBase Fire()
        {
            if (!CanFire()) return null;

            foreach (var condition in conditions)
                condition.ProcessFire();

            FireBeam();
            InvokeOnFire();

            // Hitscan: there is no projectile to hand back.
            return null;
        }

        public override bool ShouldFire(TargetingContext context)
        {
            // The AI holds the trigger to charge; ChargeTime auto-fires at full.
            if (!context.hasLineOfSight)
                return false;

            return context.distanceToTarget <= fireDistance
                && context.angleToTarget <= fireAngleTolerance;
        }

        private static readonly RaycastHit[] beamHits = new RaycastHit[16];

        private void FireBeam()
        {
            var origin = firePoint.position;
            var direction = firePoint.up;
            var end = origin + direction * range;

            // The beam starts inside the firing ship, so take the nearest hit that is NOT the
            // shooter's own hierarchy (mirrors the projectile same-root skip).
            var shooterRoot = (shooter as Component)?.transform.root;
            var count = Physics.RaycastNonAlloc(origin, direction, beamHits, range, hitMask);
            var bestIndex = -1;
            for (var i = 0; i < count; i++)
            {
                if (shooterRoot && beamHits[i].collider.transform.root == shooterRoot) continue;
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

            var shooterGameObject = (shooter as Component)?.gameObject;
            damageable.TakeDamage(damage, impactMass, firePoint.up * impactSpeed, hit.point, shooterGameObject);
        }
    }
}
