using System;
using Damage;
using Game;
using UnityEngine;
using Utils;

namespace Combat.Projectile
{
    public partial class Missile : Projectile<Missile>, IDamageable
    {
        [Header("Homing")]
        [SerializeField] private float homingSpeed    = 15f;
        [SerializeField] private float homingTurnRate = 90f;

        [Header("Explosion")]
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private float splashDamage    = 5f;
        [SerializeField] private LayerMask damageLayerMask = -1;

        [Header("Motion")]
        [SerializeField] private float initialSpeed = 15f;
        [SerializeField] private float acceleration = 40f;

        private Transform target;

        public void SetTarget(Transform tgt) => target = tgt;
        public float NormalizedSpeed => rb && homingSpeed > 0f
            ? Mathf.Clamp01(rb.linearVelocity.magnitude / homingSpeed)
            : 0f;

        public event Action OnLaunched;
        public event Action<Vector3> OnDetonated;

        protected override void Awake()
        {
            base.Awake();
            if (damageLayerMask == -1)
                damageLayerMask = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid);
        }

        public override void Initialize(IShooter shooter)
        {
            base.Initialize(shooter);

            if (rb)
            {
                var shooterVelocity = Shooter?.Velocity ?? Vector3.zero;
                rb.linearVelocity = transform.up * initialSpeed + shooterVelocity;
                rb.maxLinearVelocity = homingSpeed;
            }
            OnLaunched?.Invoke();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!rb) return;

            var normal = GamePlane.Normal;
            var currentDir = transform.up;
            var desiredDir = CalculateDesiredDirection(normal, currentDir);

            RotateTowardDirection(normal, currentDir, desiredDir);
            SteerVelocity(desiredDir);
        }
        
        private Vector3 CalculateDesiredDirection(Vector3 normal, Vector3 currentDir)
        {
            if (!target) return currentDir;

            var toTarget = Vector3.ProjectOnPlane(target.position - transform.position, normal);
            return toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : currentDir;
        }

        private void RotateTowardDirection(Vector3 normal, Vector3 currentDir, Vector3 desiredDir)
        {
            var maxTurnThisStep = homingTurnRate * Time.fixedDeltaTime;
            var signedAngle = Vector3.SignedAngle(currentDir, desiredDir, normal);
            var clampedTurn = Mathf.Clamp(signedAngle, -maxTurnThisStep, maxTurnThisStep);

            if (Mathf.Abs(clampedTurn) > 0.01f)
                transform.rotation = Quaternion.AngleAxis(clampedTurn, normal) * transform.rotation;
        }

        private void SteerVelocity(Vector3 desiredDir)
        {
            var desiredVelocity = desiredDir * homingSpeed;
            var maxTurnRad = homingTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;
            var maxAccelThisStep = acceleration * Time.fixedDeltaTime;

            rb.linearVelocity = Vector3.RotateTowards(rb.linearVelocity, desiredVelocity, maxTurnRad, maxAccelThisStep);

            if (rb.linearVelocity.magnitude > homingSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * homingSpeed;
        }

        protected override void OnHit(IDamageable other)
        {
            if (other is ProjectileBase otherProj && (otherProj.Shooter == null || otherProj.Shooter == Shooter))
                return;

            base.OnHit(other);
            Explode(other);
        }

        public void TakeDamage(float damage, float hitMass, Vector3 hitVelocity, Vector3 hitPoint, GameObject attacker)
        {
            Explode(null);
        }

        private void Explode(IDamageable directHitTarget)
        {
            var buffer = PhysicsBuffers.GetColliderBuffer(64);
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, buffer, damageLayerMask);
            var velocity = rb ? rb.linearVelocity : Vector3.zero;

            for (var i = 0; i < hitCount; i++)
            {
                var dmg = buffer[i].GetComponentInParent<IDamageable>();
                if (dmg == null) continue;
                if (Shooter != null && dmg.gameObject == Shooter.gameObject) continue;
                if (directHitTarget != null && dmg == directHitTarget) continue;

                dmg.TakeDamage(splashDamage, mass, velocity, buffer[i].ClosestPoint(transform.position), Shooter?.gameObject);
            }

            OnDetonated?.Invoke(transform.position);
            ReturnToPool();
        }

        protected override void OnReturnToPool() => target = null;
    }
} 
