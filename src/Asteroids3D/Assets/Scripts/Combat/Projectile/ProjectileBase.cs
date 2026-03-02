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
        protected IGamePlane PlaneProvider { get; private set; }

        public IShooter Shooter { get; private set; }
        public float Damage => damage;
        public float MaxDistance => maxDistance;
        public float DistanceTraveled => Vector3.Distance(startPosition, transform.position);

        public event Action Launched;
        public event Action<Vector3, IDamageable> Hit;
        public event Action ReturnedToPool;

        public virtual void Initialize(IShooter shooter)
        {
            Shooter = shooter;
            PlaneProvider = ResolvePlane(shooter);
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

        public virtual void Launch(Vector3 direction)
        {
            var frame = CurrentFrame();
            startPosition = frame.ProjectWorldPointToPlaneWorld(transform.position);
            RaiseLaunched();
        }

        protected virtual void Dispose()
        {
            ReturnToPool();
        }

        protected virtual void FixedUpdate()
        {
            PlaneConstraints.ConstrainPosition(transform, CurrentFrame());
            if (DistanceTraveled > maxDistance) Dispose();
        }

        protected virtual void OnHit(IDamageable other)
        {
            RaiseHit(other);
            ApplyDirectDamage(other);
            Dispose();
        }

        protected void OnTriggerEnter(Collider other)
        {
            var dmg = other.GetComponentInParent<IDamageable>();
            var shooterComponent = Shooter as Component;
            if (dmg == null || !shooterComponent) return;
            if (other.transform.root == shooterComponent.transform.root) return;
            if (IsFriendly(dmg)) return;
            
            OnHit(dmg);
        }

        protected void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(TagNames.Boundary)) ReturnToPool();
        }

        protected virtual void ResetState()
        {
            Shooter = null;
            PlaneProvider = null;
            if (!rb) return;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private void ApplyDirectDamage(IDamageable other)
        {
            var impactVelocity = rb ? rb.linearVelocity : Vector3.zero;
            var shooterGameObject = (Shooter as Component)?.gameObject;
            other?.TakeDamage(damage, mass, impactVelocity, transform.position, shooterGameObject);
        }

        protected bool IsFriendly(IDamageable other)
        {
            if (other == null) return false;

            var shooterComponent = Shooter as Component;
            if (shooterComponent && other.gameObject == shooterComponent.gameObject) return true;

            return other is ProjectileBase { Shooter: not null } p && p.Shooter == Shooter;
        }

        protected PlaneFrame CurrentFrame() => PlaneProvider?.CurrentFrame
            ?? throw new InvalidOperationException($"{GetType().Name} has no injected game-plane provider.");

        private static IGamePlane ResolvePlane(IShooter shooter)
        {
            if (shooter is Ships.Ship ship && ship.Plane != null)
                return ship.Plane;

            var fromContext = GameContext.SingletonOrNull?.Plane;
            if (fromContext != null)
                return fromContext;

            throw new InvalidOperationException(
                "Projectile requires an injected game-plane provider. Ensure shooter is a Ship with Plane configured.");
        }

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
            OnReturnToPool();
            ResetState();
            RaiseReturnedToPool();
            SimplePool<TSelf>.Release((TSelf)this);
        }

        protected virtual void OnReturnToPool() { }
    }
}
