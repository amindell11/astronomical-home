using Balance;
using System;
using Combat.Targeting;
using Damage;
using Movement;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    [RequireComponent(typeof(KinematicsPoller))]
    public class Missile : Projectile<Missile>, IDamageable
    {
        [Header("Homing")]
        [Stat, SerializeField] private float homingSpeed    = 15f;
        [Stat, SerializeField] private float homingTurnRate = 90f;
        [Tooltip("Full width (degrees) of the cone around the nose; a target outside it, or behind an asteroid, is lost for good.")]
        [Stat, SerializeField] internal float seekerConeAngle = 140f;
        [Tooltip("Radius of the hitbox capsule in world units: how far off the missile's centreline a shot or ship still hits it.")]
        [Stat, SerializeField] internal float hitboxRadius = 0.3f;

        [Header("Explosion")]
        [Stat, SerializeField] internal float explosionRadius = 3f;
        [Stat, SerializeField] private float splashDamage    = 5f;
        [SerializeField] private LayerMask damageLayerMask = -1;

        [Header("Motion")]
        [Stat, SerializeField] private float initialSpeed = 15f;
        [Stat, SerializeField] private float acceleration = 40f;

        [Header("Lifetime")]
        [Stat, SerializeField] private float maxLifetime = 4f;

        internal Transform target;
        private LockChannel targetChannel;
        private KinematicsPoller kinematicsPoller;
        private Vector2 prevLosDir;
        private bool hasLos;
        private float aliveTime;

        public bool IsTracking => target;

        public void SetTarget(ITargetable tgt)
        {
            target = tgt.TargetPoint;
            targetChannel = tgt.Lock;
            targetChannel.AddTrack();
        }

        protected override DamageKind Kind => DamageKind.Missile;

        public float SplashDamage => splashDamage;

        /// <summary>
        /// Configures range/lifetime at spawn (missile variants / tuning). <paramref name="maxDistance"/>
        /// is the inherited <see cref="ProjectileBase"/> travel cap; <paramref name="maxLifetime"/> is
        /// the missile's self-destruct timeout.
        /// </summary>
        public void Configure(float maxDistance, float maxLifetime)
        {
            this.maxDistance = maxDistance;
            this.maxLifetime = maxLifetime;
        }
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

            // Unity scales a Y-axis capsule's radius by the larger of the root's X and Z scales.
            var scale = transform.lossyScale;
            GetComponent<CapsuleCollider>().radius = hitboxRadius / Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        public override void Initialize(IShooter shooter)
        {
            base.Initialize(shooter);
            if (!rb) return;
            rb.maxLinearVelocity = homingSpeed;
        }

        public override void Launch(Vector3 direction, Vector3 targetPoint)
        {            
            var aim = GamePlane.WorldDirToPlane(direction);
            var shooterVelocity = GamePlane.WorldDirToPlane(Shooter?.Velocity ?? Vector3.zero);
            var inheritedAlongAim = Mathf.Max(0f, Vector2.Dot(shooterVelocity, aim));
            rb.linearVelocity = GamePlane.PlaneDirToWorld(aim * (initialSpeed + inheritedAlongAim));
            base.Launch(direction, targetPoint);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            aliveTime += Time.fixedDeltaTime;
            if (aliveTime >= maxLifetime)
            {
                Explode(null);
                Dispose();
                return;
            }

            var kin = kinematicsPoller.Kinematics;
            if (target && !CanSeeTarget(kin)) LoseTrack();
            var desiredDir = GetDesiredDirection(kin);

            ApplyTurn(kin.Forward, desiredDir);
            ApplyVelocitySteering(desiredDir);
        }

        private bool CanSeeTarget(in Kinematics kin) =>
            TargetingMath.AngleTo(kin, GamePlane.WorldPointToPlane(target.position)) <= seekerConeAngle * 0.5f
            && TargetingMath.HasLineOfSight(kin, target.position);

        private void LoseTrack()
        {
            target = null;
            targetChannel.LoseTrack();
            targetChannel = null;
        }

        private Vector2 GetDesiredDirection(Kinematics kin)
        {
            if (!target) return kin.Forward;

            var toTarget = GamePlane.WorldDirToPlane(target.position - transform.position);
            if (toTarget.sqrMagnitude < 0.01f) return kin.Forward;

            var losDir = toTarget.normalized;

            // Need 2 frames to compute LOS rate — first frame uses pure pursuit
            if (!hasLos)
            {
                prevLosDir = losDir;
                hasLos = true;
                return losDir;
            }

            // LOS rotation rate via 2D cross product (z-component)
            var losRate = (prevLosDir.x * losDir.y - prevLosDir.y * losDir.x) / Time.fixedDeltaTime;
            prevLosDir = losDir;

            // PN: lateral acceleration = N * speed * LOS_rate
            const float navGain = 4f;
            var speed = kin.Speed > 0.1f ? kin.Speed : homingSpeed;
            var accelCmd = navGain * speed * losRate;

            // Convert to desired direction: current heading + lateral correction
            var lateral = new Vector2(-kin.Forward.y, kin.Forward.x);
            var desired = kin.Forward * speed + lateral * accelCmd * Time.fixedDeltaTime;
            return desired.sqrMagnitude > 0.01f ? desired.normalized : kin.Forward;
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

        public void TakeDamage(in DamageInfo hit)
        {
            Explode(null);
            Dispose();
        }

        private void ApplySplashDamage(Vector3 pos, float radius, int layerMask, IDamageable directHitTarget)
        {
            var buffer = PhysicsBuffers.GetColliderBuffer(64);
            var hitCount = Physics.OverlapSphereNonAlloc(pos, radius, buffer, layerMask);
            var velocity = rb ? rb.linearVelocity : Vector3.zero;
            var attackerId = Shooter?.Id ?? Ships.Registry.ShipId.Invalid;
            for (var i = 0; i < hitCount; i++)
            {
                var obj = buffer[i].GetComponentInParent<IDamageable>();
                if (obj == null || obj == directHitTarget || IsFriendly(obj)) continue;

                obj.TakeDamage(new DamageInfo(splashDamage, DamageKind.Missile, attackerId,
                    mass, velocity, buffer[i].ClosestPoint(transform.position)));
            }
        }

        private void Explode(IDamageable directHitTarget)
        {
            ApplySplashDamage(transform.position, explosionRadius, damageLayerMask, directHitTarget);
            OnDetonated?.Invoke(transform.position);
        }

        protected override void OnReturnToPool()
        {
            targetChannel?.EndTrack();
            targetChannel = null;
            target = null;
            hasLos = false;
            aliveTime = 0f;
        }
    }
} 
