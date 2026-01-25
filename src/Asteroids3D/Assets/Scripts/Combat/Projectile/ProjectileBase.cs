using System;
using Damage;
using Game;
using UnityEngine;
using Utils;

namespace Combat.Projectile
{
    public abstract class ProjectileBase : MonoBehaviour
    {
        [Header("Base Projectile Settings")]
        [SerializeField] protected float damage      = 10f;
        [SerializeField] protected float maxDistance = 50f;
        [SerializeField] protected float mass        = 0.1f;

        protected Rigidbody rb;
        protected Vector3 startPosition;

        public IShooter Shooter { get; protected set; }
        public float Damage => damage;
        public float MaxDistance => maxDistance;
        public float DistanceTraveled => Vector3.Distance(startPosition, transform.position);

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
            startPosition = transform.position;

            if (!rb) return;
            rb.useGravity = false;
            rb.mass = mass;
        }

        protected virtual void FixedUpdate()
        {
            transform.position = GamePlane.ProjectOntoPlane(transform.position);

            if (DistanceTraveled > maxDistance)
                ReturnToPool();
        }

        protected virtual void OnHit(IDamageable other)
        {
            Hit?.Invoke(transform.position, other);

            if (other is ProjectileBase { Shooter: not null } otherProj && otherProj.Shooter == Shooter)
                return;

            var impactVelocity = rb ? rb.linearVelocity : Vector3.zero;
            other?.TakeDamage(damage, mass, impactVelocity, transform.position, Shooter?.gameObject);
            ReturnToPool();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            var dmg = other.GetComponentInParent<IDamageable>();
            if (dmg == null) return;
            if (Shooter != null && other.GetComponentInParent<IShooter>() == Shooter) return;

            OnHit(dmg);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(TagNames.Boundary)) ReturnToPool();
        }

        protected virtual void ResetState()
        {
            Shooter = null;
            if (!rb) return;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        protected abstract void ReturnToPool();

        protected void RaiseReturnedToPool()
        {
            ReturnedToPool?.Invoke();
        }
    }

    /// <summary>
    /// Generic projectile base that handles object pooling automatically.
    /// Derived classes only need to override OnReturnToPool for type-specific cleanup.
    /// </summary>
    public abstract class Projectile<TSelf> : ProjectileBase where TSelf : Projectile<TSelf>
    {
        protected sealed override void ReturnToPool()
        {
            OnReturnToPool();
            ResetState();
            RaiseReturnedToPool();
            SimplePool<TSelf>.Release((TSelf)this);
        }

        protected virtual void OnReturnToPool() { }
    }
}
