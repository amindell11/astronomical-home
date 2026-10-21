using Balance;
using System;
using Damage;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    public abstract class ProjectileBase : MonoBehaviour
    {
        [Header("Base Projectile Settings")]
        [Stat, SerializeField] protected float damage      = 10f;
        [Stat, SerializeField] protected float maxDistance = 50f;
        [SerializeField] protected float mass        = 0.1f;

        protected internal Rigidbody rb;
        protected Vector3 startPosition;

        public IShooter Shooter { get; private set; }
        protected abstract DamageKind Kind { get; }
        public float Damage => damage;
        public float MaxDistance => maxDistance;
        public float DistanceTraveled => Vector3.Distance(startPosition, transform.position);

        public event Action Launched;
        public event Action<Vector3, IDamageable> Hit;
        public event Action ReturnedToPool;

        public virtual void Initialize(IShooter shooter)
        {
            Shooter = shooter;
        }

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        protected virtual void OnEnable()
        {
            if (!rb) return;
            rb.useGravity = false;
            rb.mass = mass;
        }

        /// <summary>Sends the projectile off along <paramref name="direction"/>; only self-steering projectiles read <paramref name="targetPoint"/>.</summary>
        public virtual void Launch(Vector3 direction, Vector3 targetPoint)
        {
            PlaneConstraints.ConstrainPosition(transform);
            startPosition = transform.position;
            RaiseLaunched();
        }

        protected virtual void Dispose()
        {
            ReturnToPool();
        }

        protected virtual void FixedUpdate()
        {
            PlaneConstraints.ConstrainPosition(transform);
            if (DistanceTraveled > maxDistance)
            {
                Dispose();
                return;
            }

            SweepStep();
        }

        // Trigger-collider callbacks see only end-of-step overlaps; a fast shot can cross a thin target unseen.
        private void SweepStep()
        {
            var velocity = rb.linearVelocity;
            var speed = velocity.magnitude;
            if (speed <= 0f) return;

            var direction = velocity / speed;
            if (!rb.SweepTest(direction, out var hit, speed * Time.fixedDeltaTime, QueryTriggerInteraction.Collide)) return;
            if (!TryGetHitTarget(hit.collider, out var target)) return;

            transform.position += direction * hit.distance;
            OnHit(target);
        }

        protected virtual void OnHit(IDamageable other)
        {
            RaiseHit(other);
            ApplyDirectDamage(other);
            Dispose();
        }

        protected void OnTriggerEnter(Collider other)
        {
            if (TryGetHitTarget(other, out var target)) OnHit(target);
        }

        private bool TryGetHitTarget(Collider other, out IDamageable target)
        {
            target = other.GetComponentInParent<IDamageable>();
            var shooterComponent = Shooter as Component;
            if (target == null || !shooterComponent) return false;
            if (other.attachedRigidbody && other.attachedRigidbody == Shooter.Body) return false;
            return !IsFriendly(target);
        }

        protected virtual void ResetState()
        {
            Shooter = null;
            damageScale = 1f;
            if (!rb) return;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>Per-shot multiplier set by the firing weapon after launch (charge-scaled shots); resets to 1 on pool return.</summary>
        public void SetDamageScale(float scale)
        {
            damageScale = Mathf.Max(0f, scale);
        }

        internal float DamageScale => damageScale;

        private float damageScale = 1f;

        private void ApplyDirectDamage(IDamageable other)
        {
            var impactVelocity = rb ? rb.linearVelocity : Vector3.zero;
            var attackerId = Shooter?.Id ?? Ships.Registry.ShipId.Invalid;
            other?.TakeDamage(new DamageInfo(damage * damageScale, Kind, attackerId,
                mass, impactVelocity, transform.position));
        }

        protected bool IsFriendly(IDamageable other)
        {
            if (other == null) return false;

            var shooterComponent = Shooter as Component;
            if (shooterComponent && other.gameObject == shooterComponent.gameObject) return true;

            return other is ProjectileBase { Shooter: not null } p && p.Shooter == Shooter && !p.TakesOwnerFire;
        }

        /// <summary>True for a projectile its own shooter's fire can hit (a charge the owner pops early).</summary>
        protected virtual bool TakesOwnerFire => false;

        /// <summary>Immediately returns this projectile to its pool with no detonation or hit effects (episode/scene flush).</summary>
        public void ReturnToPoolImmediate() => ReturnToPool();

        protected abstract void ReturnToPool();

        protected void RaiseReturnedToPool()
        {
            ReturnedToPool?.Invoke();
        }

        protected void RaiseHit(IDamageable other)
        {
            Hit?.Invoke(transform.position, other);
        }

        private void RaiseLaunched()
        {
            Launched?.Invoke();
        }
    }
    
    public abstract class Projectile<TSelf> : ProjectileBase where TSelf : Projectile<TSelf>
    {
        protected sealed override void ReturnToPool()
        {
            // One physics step can deliver several death callbacks; honor only the first return per activation.
            if (!gameObject.activeSelf) return;
            OnReturnToPool();
            ResetState();
            RaiseReturnedToPool();
            SimplePool<TSelf>.Release((TSelf)this);
        }

        protected virtual void OnReturnToPool() { }
    }
}
