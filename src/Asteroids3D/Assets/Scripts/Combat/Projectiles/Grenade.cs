using System;
using Damage;
using Game;
using UnityEngine;
using Utils;

namespace Combat.Projectile
{
    /// <summary>Drop-behind concussion charge, detonated by fuse expiry, armed contact, or being shot: detonation spawns a <see cref="ConcussionWave"/> — the grenade itself never applies damage.</summary>
    public class Grenade : Projectile<Grenade>, IDamageable, ITransientSpawner
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

        protected override DamageKind Kind => DamageKind.ConcussionWave;

        public event Action<Vector3> OnDetonated;

        /// <summary>Announces the detonation's wave so whoever tracks this grenade tracks the wave too (<see cref="ITransientSpawner"/>).</summary>
        public event Action<MonoBehaviour, Action> Spawned;

        public bool Armed => aliveTime >= armingSeconds;

        /// <summary>Serialized-state reads for hangar stat lines (evaluated on the prefab asset).</summary>
        public float FuseSeconds => fuseSeconds;
        public ConcussionWave WavePrefab => wavePrefab;

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

        public void TakeDamage(in DamageInfo hit)
        {
            Detonate();
        }

        private void Detonate()
        {
            if (detonated) return;
            detonated = true;

            if (wavePrefab)
            {
                var wave = SimplePool<ConcussionWave>.Get(wavePrefab, transform.position, Quaternion.identity);
                wave.Begin(Shooter?.Id ?? Ships.ShipId.Invalid);
                Spawned?.Invoke(wave, wave.ReturnToPoolImmediate);
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
