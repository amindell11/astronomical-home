using Balance;
using System;
using System.Collections.Generic;
using Damage;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    /// <summary>Expanding concussion wavefront: each damageable is hit exactly once as the frontier first overlaps it, damage/impulse scaled by <see cref="Falloff"/>; deliberately hits everything — shooter included (kept only for kill attribution) — so the dropper must outrun their own blast.</summary>
    public class ConcussionWave : MonoBehaviour
    {
        [Header("Wave")]
        [Stat, SerializeField, Min(0.01f)] private float maxRadius = 12f;
        [Stat, SerializeField, Min(0.01f)] private float expandSpeed = 20f;

        [Header("Effect")]
        [Stat, SerializeField, Min(0f)] private float maxDamage = 50f;
        [Tooltip("Falloff curve exponent: 1 = linear; below 1 holds damage up toward the rim.")]
        [Stat, SerializeField, Min(0.01f)] private float damageFalloffPower = 1f;
        [Tooltip("Impulse at the center, applied where the wavefront meets each hull; spin comes from that offset.")]
        [Stat, SerializeField, Min(0f)] private float impulse = 8000f;
        [Tooltip("Falloff curve exponent: 1 = linear; above 1 concentrates the push at the center.")]
        [Stat, SerializeField, Min(0.01f)] private float impulseFalloffPower = 1f;
        [SerializeField, Min(0f)] private float waveMass = 1f;
        [SerializeField] private LayerMask sweepMask = -1;

        private readonly HashSet<Collider> resolved = new();
        private readonly HashSet<IDamageable> swept = new();
        private float radius;
        private int hullLayers;
        private Ships.Registry.ShipId attackerId;

        public float Radius => radius;
        public float MaxRadius => maxRadius;
        public float MaxDamage => maxDamage;

        private void Awake()
        {
            hullLayers = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid);
            if (sweepMask == -1)
                sweepMask = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid, LayerIds.Projectile, LayerIds.Missile);
        }

        /// <summary>Raised by <see cref="Begin"/> — a live detonation, unlike OnEnable, which pool warmup also triggers.</summary>
        public event Action Begun;

        /// <summary>Raised just before every pool return (spent frontier or flush), so trackers never hold a stale registration for an alive-but-pooled wave.</summary>
        public event Action Released;

        /// <summary>Starts a sweep from this transform's position, attributing damage to <paramref name="attackerId"/>.</summary>
        public void Begin(Ships.Registry.ShipId attackerId)
        {
            this.attackerId = attackerId;
            radius = 0f;
            resolved.Clear();
            swept.Clear();
            Begun?.Invoke();
        }

        private void FixedUpdate()
        {
            radius += expandSpeed * Time.fixedDeltaTime;
            Sweep();

            if (radius >= maxRadius)
                ReturnToPoolImmediate();
        }

        /// <summary>Immediately returns this wave to its pool, ending the sweep (episode/scene flush).</summary>
        public void ReturnToPoolImmediate()
        {
            Released?.Invoke();
            SimplePool<ConcussionWave>.Release(this);
        }

        private void Sweep()
        {
            var damageFalloff = Falloff(radius, maxRadius, damageFalloffPower);
            var impulseFalloff = Falloff(radius, maxRadius, impulseFalloffPower);

            // Already-swept inner colliders would crowd a fixed-size result and starve newly reached outer targets — regrow until the query fits.
            var buffer = PhysicsBuffers.GetColliderBuffer(64);
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, sweepMask);
            while (hitCount == buffer.Length)
            {
                buffer = PhysicsBuffers.GetColliderBuffer(buffer.Length * 2);
                hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, sweepMask);
            }

            for (var i = 0; i < hitCount; i++)
            {
                // The disc re-overlaps every swept collider each step; resolve each only once.
                if (!resolved.Add(buffer[i])) continue;
                // A trigger on a hull layer is a broadphase volume (an asteroid's bounding sphere), not the hull.
                if (buffer[i].isTrigger && (hullLayers & (1 << buffer[i].gameObject.layer)) != 0) continue;

                var target = buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || !swept.Add(target)) continue;

                var hitPoint = buffer[i].ClosestPoint(transform.position);
                var outward = OutwardDirection(hitPoint, target.gameObject.transform.position);
                Push(buffer[i].attachedRigidbody, outward, hitPoint, impulseFalloff);
                target.TakeDamage(new DamageInfo(maxDamage * damageFalloff, DamageKind.ConcussionWave, attackerId,
                    waveMass, outward * expandSpeed, hitPoint));
            }
        }

        // Blast center to hull point; a center inside the hull falls back to the target's position.
        private Vector3 OutwardDirection(Vector3 hitPoint, Vector3 targetPosition)
        {
            var planar = GamePlane.WorldDirToPlane(hitPoint - transform.position);
            if (planar.sqrMagnitude < 0.0001f)
                planar = GamePlane.WorldDirToPlane(targetPosition - transform.position);
            return planar.sqrMagnitude < 0.0001f
                ? GamePlane.PlaneDirToWorld(Vector2.up)
                : GamePlane.PlaneDirToWorld(planar.normalized);
        }

        private void Push(Rigidbody body, Vector3 outward, Vector3 hitPoint, float falloff)
        {
            if (body && !body.isKinematic)
                body.AddForceAtPosition(outward * (impulse * falloff), hitPoint, ForceMode.Impulse);
        }

        /// <summary>Damage/impulse scale at a frontier radius: 1 at the center, 0 at max radius, shaped by <paramref name="power"/>.</summary>
        internal static float Falloff(float radius, float maxRadius, float power)
        {
            return maxRadius <= 0f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - radius / maxRadius), power);
        }
    }
}
