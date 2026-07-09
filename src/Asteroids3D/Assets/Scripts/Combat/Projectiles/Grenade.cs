using System;
using Damage;
using Game;
using UnityEngine;
using Utils;

namespace Combat.Projectile
{
    /// <summary>
    /// A drop-behind concussion charge: released with the shooter's velocity minus a backward
    /// push, parked near the drop point by rigidbody damping, then detonated by fuse expiry,
    /// armed contact, or being shot. Detonation spawns a <see cref="ConcussionWave"/>; the
    /// grenade itself never applies damage.
    /// </summary>
    public class Grenade : Projectile<Grenade>, IDamageable
    {
        [Header("Charge")]
        [SerializeField, Min(0f)] private float fuseSeconds = 2.5f;
        [Tooltip("Grace period before contact can detonate the charge (fuse and gunfire always can).")]
        [SerializeField, Min(0f)] private float armingSeconds = 0.3f;
        [Tooltip("Backward push relative to the fire direction at release.")]
        [SerializeField, Min(0f)] private float dropSpeed = 3f;

        [Header("Blast")]
        [SerializeField] private ConcussionWave wavePrefab;

        private float aliveTime;
        private bool detonated;

        public event Action<Vector3> OnDetonated;

        public bool Armed => aliveTime >= armingSeconds;

        /// <summary>Deterministic test seam for the fuse/arming timers; resets the clock.</summary>
        public void Configure(float fuseSeconds, float armingSeconds)
        {
            this.fuseSeconds = fuseSeconds;
            this.armingSeconds = armingSeconds;
            aliveTime = 0f;
        }

        protected override void Awake()
        {
            base.Awake();
            if (wavePrefab)
                SimplePool<ConcussionWave>.Warm(wavePrefab);
        }

        public override void Launch(Vector3 direction)
        {
            if (rb)
            {
                var shooterVelocity = GamePlane.WorldDirToPlane(Shooter?.Velocity ?? Vector3.zero);
                var dropVelocity = shooterVelocity - GamePlane.WorldDirToPlane(direction) * dropSpeed;
                rb.linearVelocity = GamePlane.PlaneDirToWorld(dropVelocity);
            }
            base.Launch(direction);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            aliveTime += Time.fixedDeltaTime;
            if (aliveTime >= fuseSeconds)
                Detonate();
        }

        protected override void OnHit(IDamageable other)
        {
            if (!Armed) return;
            RaiseHit(other);
            Detonate();
        }

        public void TakeDamage(float damage, float hitMass, Vector3 hitVelocity, Vector3 hitPoint, GameObject attacker)
        {
            Detonate();
        }

        private void Detonate()
        {
            if (detonated) return;
            detonated = true;

            if (wavePrefab)
            {
                var shooterComponent = Shooter as Component;
                var attacker = shooterComponent ? shooterComponent.gameObject : null;
                SimplePool<ConcussionWave>.Get(wavePrefab, transform.position, Quaternion.identity)
                    .Begin(attacker);
            }

            OnDetonated?.Invoke(transform.position);
            Dispose();
        }

        protected override void OnReturnToPool()
        {
            aliveTime = 0f;
            detonated = false;
        }
    }
}
