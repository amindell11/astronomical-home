using Balance;
using System;
using Damage;
using UnityEngine;
using Utils;
using Substrate;

namespace Combat.Projectiles
{
    /// <summary>
    /// Concussion charge flown as a reverse missile: launched at a fixed speed straight at its
    /// target point (clamped to <see cref="ProjectileBase.MaxDistance"/>), it coasts until the point
    /// is within its braking distance, then brakes by its own thrust to rest on that point.
    /// Detonates on arrival, on contact (never the owner's hull), or when shot — the owner's fire
    /// included. Detonation spawns a <see cref="ConcussionWave"/>; the charge itself never applies
    /// damage. Design: arc #950.
    /// </summary>
    public class Grenade : Projectile<Grenade>, IDamageable, ITransientSpawner, IChargeFlight
    {
        [Header("Charge")]
        [Stat, SerializeField, Min(0.01f)] private float launchSpeed = 40f;
        [Tooltip("Distance before the target point where the charge stops coasting and brakes. At max range or above, it brakes from launch.")]
        [Stat, SerializeField, Min(0f)] private float brakingDistance = 10f;

        [Header("Blast")]
        [Stat, SerializeField] private ConcussionWave wavePrefab;

        private Vector3 targetPoint;
        private Vector3 heading;
        private bool braking;
        private bool detonated;

        protected override DamageKind Kind => DamageKind.ConcussionWave;
        protected override bool TakesOwnerFire => true;

        public event Action<Vector3> OnDetonated;

        /// <summary>Raised once per flight, on the first step the charge thrusts against its heading.</summary>
        public event Action BrakingStarted;

        /// <summary>Announces the detonation's wave so whoever tracks this grenade tracks the wave too (<see cref="ITransientSpawner"/>).</summary>
        public event Action<MonoBehaviour, Action> Spawned;

        /// <summary>Where this charge comes to rest: the launch's target point, clamped to max range.</summary>
        public Vector3 TargetPoint => targetPoint;

        /// <summary>World direction of flight, fixed at launch.</summary>
        public Vector3 Heading => heading;

        /// <summary>Serialized-state read for hangar stat lines (evaluated on the prefab asset).</summary>
        public ConcussionWave WavePrefab => wavePrefab;

        public float BlastRadius => wavePrefab.MaxRadius;

        event Action IChargeFlight.Ended
        {
            add => ReturnedToPool += value;
            remove => ReturnedToPool -= value;
        }

        protected override void Awake()
        {
            base.Awake();
            SimplePool<ConcussionWave>.Warm(wavePrefab);
        }

        public override void Launch(Vector3 direction, Vector3 targetPoint)
        {
            var origin = GamePlane.WorldPointToPlane(transform.position);
            var offset = GamePlane.WorldPointToPlane(targetPoint) - origin;
            var planarHeading = offset.sqrMagnitude > 0.0001f ? offset.normalized : GamePlane.WorldDirToPlane(direction).normalized;

            heading = GamePlane.PlaneDirToWorld(planarHeading);
            this.targetPoint = GamePlane.PlanePointToWorld(origin + planarHeading * Mathf.Min(offset.magnitude, maxDistance));
            rb.linearVelocity = heading * launchSpeed;
            base.Launch(direction, targetPoint);
        }

        protected override void FixedUpdate()
        {
            var remaining = Vector3.Dot(targetPoint - transform.position, heading);
            var closingSpeed = Vector3.Dot(rb.linearVelocity, heading);
            if (remaining <= 0f || closingSpeed <= 0f)
            {
                Detonate();
                return;
            }

            if (remaining > brakingDistance)
            {
                base.FixedUpdate();
                return;
            }

            if (!braking)
            {
                braking = true;
                BrakingStarted?.Invoke();
            }

            rb.AddForce(-heading * BrakingDeceleration(closingSpeed, remaining, Time.fixedDeltaTime), ForceMode.Acceleration);
            base.FixedUpdate();
        }

        // Discrete-step form of v²/2d, which stops about v·dt/2 short; re-solved each step it holds constant.
        internal static float BrakingDeceleration(float speed, float distance, float dt) =>
            speed * speed / (2f * distance + speed * dt);

        protected override void OnHit(IDamageable other)
        {
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

            var wave = SimplePool<ConcussionWave>.Get(wavePrefab, transform.position, Quaternion.identity);
            wave.Begin(Shooter?.Id ?? Ships.Registry.ShipId.Invalid);
            Spawned?.Invoke(wave, wave.ReturnToPoolImmediate);

            OnDetonated?.Invoke(transform.position);
            Dispose();
        }

        protected override void OnReturnToPool()
        {
            braking = false;
            detonated = false;
        }
    }
}
