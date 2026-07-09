using System;
using System.Collections.Generic;
using Damage;
using Game;
using UnityEngine;
using Utils;

namespace Combat.Projectile
{
    /// <summary>
    /// An expanding concussion wavefront: the radius grows at <see cref="expandSpeed"/> and each
    /// damageable is hit exactly once, when the frontier first overlaps it, with damage and an
    /// outward impulse scaled by <see cref="Falloff"/> at that radius. Deliberately hits
    /// everything — shooter included — so the dropper must outrun their own blast; the shooter
    /// is kept only for kill attribution.
    /// </summary>
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
                SimplePool<ConcussionWave>.Release(this);
        }

        private void Sweep()
        {
            var falloff = Falloff(radius, maxRadius);

            // Already-swept inner colliders stay inside the growing sphere and would permanently
            // crowd a fixed-size result, starving newly reached outer targets — regrow until the
            // query fits.
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
