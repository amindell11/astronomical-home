using Balance;
using System;
using System.Collections.Generic;
using Combat.Projectiles;
using Damage;
using UnityEngine;
using Substrate.Services.Projectiles;
using Combat.Weapons.Conditions;
using Substrate;

namespace Combat.Weapons.Arsenal
{
    /// <summary>
    /// Charged hitscan railgun (<see cref="ProjectileSpeed"/> 0 — AI gunners aim with no intercept lead).
    /// The beam is a capsule swept along the game plane that pierces: it strikes targets nearest-first,
    /// and each kill's overkill (the return of <see cref="IDamageable.TakeDamage"/>) carries on to the
    /// next target until a survivor, or a collider that takes no damage, stops it.
    /// </summary>
    public class Railguns : WeaponComponent
    {
        [Header("Beam")]
        [Tooltip("Damage the beam carries. It pierces: each target it kills passes the leftover damage on to the next one behind it.")]
        [Stat, SerializeField, Min(0f)] private float damage = 45f;
        [Tooltip("Max beam length.")]
        [Stat, SerializeField, Min(0f)] private float range = 60f;
        [Tooltip("Beam half-width: the beam strikes any collider within this distance of its line.")]
        [Stat, SerializeField, Min(0f)] private float beamRadius = 0.5f;
        [Tooltip("Impact speed the beam reports on hit; with impactMass it sets the momentum share and impact direction when the beam breaks an asteroid.")]
        [SerializeField, Min(0f)] private float impactSpeed = 120f;
        [Tooltip("Impact mass the beam reports on hit; with impactSpeed it sets how much momentum fragments take when the beam breaks an asteroid.")]
        [SerializeField, Min(0f)] private float impactMass = 0.05f;
        [Tooltip("Layers the beam can hit. -1 = Ship | Asteroid.")]
        [SerializeField] private LayerMask hitMask = -1;

        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will hold the charge trigger.")]
        [Stat, SerializeField, Min(0f)] private float fireDistance = 45f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will hold the charge trigger.")]
        [Stat, SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 2f;

        /// <summary>Raised per shot with the beam's world start and end points (for visuals).</summary>
        public event Action<Vector3, Vector3> OnBeamFired;

        /// <summary>Per beam hit: the point and what it struck, null when the collider takes no damage.</summary>
        public event Action<Vector3, IDamageable> Hit;

        [Header("Conditions")]
        [Stat, SerializeField] private ChargeTime charge;
        [Stat, SerializeField] private Cooldown cooldown;

        public ChargeTime Charge => charge;
        public float Damage => damage;
        public float BeamRange => range;
        public float BeamRadius => beamRadius;

        public override float ProjectileSpeed => 0f;

        public override float FireRange => fireDistance;

        protected override void Awake()
        {
            base.Awake();
            if (!charge) charge = GetComponent<ChargeTime>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
            if (hitMask == -1)
                hitMask = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid);
        }

        public override void HandleTrigger(in WeaponCommand cmd, IProjectileService projectiles)
        {
            if (Charge && Charge.HandleTrigger(cmd.held, Time.fixedDeltaTime))
                Fire(cmd.targetPoint, projectiles);
        }

        // Hitscan: the registry is part of the uniform firing surface but there is never a projectile to track.
        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
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

        private static RaycastHit[] beamHits = new RaycastHit[32];
        private static readonly IComparer<RaycastHit> nearestFirst =
            Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));
        private static readonly HashSet<IDamageable> struck = new();

        private void FireBeam()
        {
            var origin = GamePlane.PlanePointToWorld(GamePlane.WorldPointToPlane(firePoint.position));
            var direction = GamePlane.PlaneDirToWorld(GamePlane.WorldDirToPlane(firePoint.up).normalized);
            var end = origin + direction * range;

            // A full buffer may have dropped the nearest hits, so grow it and cast again.
            int count;
            while ((count = Physics.SphereCastNonAlloc(origin, beamRadius, direction, beamHits, range, hitMask)) == beamHits.Length)
                beamHits = new RaycastHit[beamHits.Length * 2];
            Array.Sort(beamHits, 0, count, nearestFirst);

            // The beam starts inside the firing ship, so skip the shooter's own body.
            var shooterBody = shooter?.Body;
            var attackerId = shooter?.Id ?? Ships.Registry.ShipId.Invalid;
            var remaining = damage;
            struck.Clear();
            for (var i = 0; i < count; i++)
            {
                var hit = beamHits[i];
                if (shooterBody && hit.rigidbody == shooterBody) continue;
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !struck.Add(damageable)) continue;

                // A collider already overlapping the sphere at the origin reports distance 0 and a zero point.
                var point = hit.distance > 0f ? hit.point : hit.collider.ClosestPoint(origin);
                remaining = damageable?.TakeDamage(new DamageInfo(remaining, DamageKind.Railgun, attackerId,
                    impactMass, direction * impactSpeed, point)) ?? 0f;
                Hit?.Invoke(point, damageable);
                if (remaining > 0f) continue;

                end = origin + direction * Mathf.Max(0f, Vector3.Dot(point - origin, direction));
                break;
            }

            OnBeamFired?.Invoke(origin, end);
        }
    }
}
