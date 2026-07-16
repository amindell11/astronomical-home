using System;
using System.Collections.Generic;
using Damage;
using Game;
using UnityEngine;
using Utils;

namespace Combat.Projectile
{
    /// <summary>Expanding concussion wavefront: each damageable is hit exactly once as the frontier first overlaps it, damage/impulse scaled by <see cref="Falloff"/>; deliberately hits everything — shooter included (kept only for kill attribution) — so the dropper must outrun their own blast.</summary>
    public class ConcussionWave : MonoBehaviour
    {
        [Header("Wave")]
        [SerializeField, Min(0.01f)] private float maxRadius = 12f;
        [SerializeField, Min(0.01f)] private float expandSpeed = 20f;

        [Header("Effect")]
        [SerializeField, Min(0f)] private float maxDamage = 40f;
        [SerializeField, Min(0f)] private float impulse = 8f;
        [SerializeField, Min(0f)] private float waveMass = 1f;
        [SerializeField] private LayerMask sweepMask = -1;

        private readonly HashSet<Collider> resolved = new();
        private readonly HashSet<IDamageable> swept = new();
        private float radius;
        private GameObject attacker;

        public float Radius => radius;
        public float MaxRadius => maxRadius;
        public float MaxDamage => maxDamage;

        private void Awake()
        {
            if (sweepMask == -1)
                sweepMask = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid, LayerIds.Projectile, LayerIds.Missile);
        }

        /// <summary>Raised by <see cref="Begin"/> — a live detonation, unlike OnEnable, which pool warmup also triggers.</summary>
        public event Action Begun;

        /// <summary>Raised just before every pool return (spent frontier or flush), so trackers never hold a stale registration for an alive-but-pooled wave.</summary>
        public event Action Released;

        /// <summary>Starts a sweep from this transform's position, attributing damage to <paramref name="attacker"/>.</summary>
        public void Begin(GameObject attacker)
        {
            this.attacker = attacker;
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
            var falloff = Falloff(radius, maxRadius);

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

                var target = buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || !swept.Add(target)) continue;

                var outward = OutwardDirection(target.gameObject.transform.position);
                Push(buffer[i].attachedRigidbody, outward, falloff);
                target.TakeDamage(maxDamage * falloff, waveMass, outward * expandSpeed,
                    buffer[i].ClosestPoint(transform.position), attacker);
            }
        }

        private Vector3 OutwardDirection(Vector3 targetPosition)
        {
            var planar = GamePlane.WorldDirToPlane(targetPosition - transform.position);
            return planar.sqrMagnitude < 0.0001f
                ? GamePlane.PlaneDirToWorld(Vector2.up)
                : GamePlane.PlaneDirToWorld(planar.normalized);
        }

        private void Push(Rigidbody body, Vector3 outward, float falloff)
        {
            if (body && !body.isKinematic)
                body.AddForce(outward * (impulse * falloff), ForceMode.Impulse);
        }

        /// <summary>Linear damage/impulse scale at a frontier radius: 1 at the center, 0 at max radius.</summary>
        internal static float Falloff(float radius, float maxRadius)
        {
            return maxRadius <= 0f ? 0f : Mathf.Clamp01(1f - radius / maxRadius);
        }
    }
}
