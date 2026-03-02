using System;
using Damage;
using Game;
using Movement;
using Ships.Movement;
using UnityEngine;
using Utils;

namespace Combat.Projectile
{
    [RequireComponent(typeof(KinematicsPoller))]
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
        private KinematicsPoller kinematicsPoller;

        public void SetTarget(Transform tgt) => target = tgt;
        public float NormalizedSpeed => rb && homingSpeed > 0f
            ? Mathf.Clamp01(rb.linearVelocity.magnitude / homingSpeed)
            : 0f;

        public event Action<Vector3> OnDetonated;

        protected override void Awake()
        {
            base.Awake();
            kinematicsPoller = GetComponent<KinematicsPoller>();
            if (!kinematicsPoller) kinematicsPoller = gameObject.AddComponent<KinematicsPoller>();
            if (damageLayerMask == -1)
                damageLayerMask = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid);
        }

        public override void Initialize(IShooter shooter)
        {
            base.Initialize(shooter);
            if (!rb) return;
            rb.maxLinearVelocity = homingSpeed;
        }

        public override void Launch(Vector3 direction)
        {            
            var shooterVelocity = GamePlane.WorldDirToPlane(Shooter?.Velocity ?? Vector3.zero);
            var launchVelocity = GamePlane.WorldDirToPlane(direction) * initialSpeed + shooterVelocity;
            rb.linearVelocity = GamePlane.PlaneDirToWorld(launchVelocity);
            base.Launch(direction);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            var kin = kinematicsPoller.Kinematics;
            var desiredDir = GetDesiredDirection(kin);

            ApplyTurn(kin.Forward, desiredDir);
            ApplyVelocitySteering(desiredDir);
        }


        private Vector2 GetDesiredDirection(Kinematics kin)
        {
            if (!target) return kin.Forward;
            var toTarget = GamePlane.WorldDirToPlane(target.position - transform.position);
            return toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : kin.Forward;
        }

        private void ApplyTurn(Vector2 currentDir, Vector2 desiredDir)
        {
            var maxTurnThisStep = homingTurnRate * Time.fixedDeltaTime;
            var signedAngle = Vector2.SignedAngle(currentDir, desiredDir);
            var clampedTurn = Mathf.Clamp(signedAngle, -maxTurnThisStep, maxTurnThisStep);

            if (Mathf.Abs(clampedTurn) > 0.01f)
                transform.rotation = Quaternion.AngleAxis(clampedTurn, GamePlane.Normal) * transform.rotation;
        }

        private void ApplyVelocitySteering(Vector2 desiredDir)
        {
            var desiredVelocity = GamePlane.PlaneDirToWorld(desiredDir * homingSpeed);
            var maxTurnRad = homingTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;
            var maxAccelThisStep = acceleration * Time.fixedDeltaTime;

            rb.linearVelocity = Vector3.RotateTowards(rb.linearVelocity, desiredVelocity, maxTurnRad, maxAccelThisStep);

            if (rb.linearVelocity.magnitude > homingSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * homingSpeed;
        }

        protected override void OnHit(IDamageable other)
        {
            Explode(other);
            base.OnHit(other);
        }

        public void TakeDamage(float damage, float hitMass, Vector3 hitVelocity, Vector3 hitPoint, GameObject attacker)
        {
            Explode(null);
            Dispose();
        }

        private void ApplySplashDamage(Vector3 pos, float radius, int layerMask, IDamageable directHitTarget)
        {
            var buffer = PhysicsBuffers.GetColliderBuffer(64);
            var hitCount = Physics.OverlapSphereNonAlloc(pos, radius, buffer, layerMask);
            var velocity = rb ? rb.linearVelocity : Vector3.zero;
            for (var i = 0; i < hitCount; i++)
            {
                var obj = buffer[i].GetComponentInParent<IDamageable>();
                if (obj == null || obj == directHitTarget || IsFriendly(obj)) continue;

                var shooterGameObject = (Shooter as Component) ? (Shooter as Component).gameObject : null;
                obj.TakeDamage(splashDamage, mass, velocity, buffer[i].ClosestPoint(transform.position), shooterGameObject);
            }
        }

        private void Explode(IDamageable directHitTarget)
        {
            ApplySplashDamage(transform.position, explosionRadius, damageLayerMask, directHitTarget);
            OnDetonated?.Invoke(transform.position);
        }

        protected override void OnReturnToPool() => target = null;
    }
} 
