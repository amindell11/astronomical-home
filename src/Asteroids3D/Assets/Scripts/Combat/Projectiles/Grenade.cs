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
    /// target point (clamped to <see cref="ProjectileBase.MaxDistance"/>), it brakes by its own
    /// thrust to rest on that point. Being shot detonates it at once — the owner's fire included.
    /// Every other fuse waits for <see cref="armingTime"/> after launch: then it detonates on
    /// arrival (holding on the point if it got there first), on contact, or when a ship comes
    /// within <see cref="proximityRadius"/> — never the owner's hull. Detonation spawns a
    /// <see cref="ConcussionWave"/>; the charge itself never applies damage. Design: arc #950.
    /// </summary>
    public class Grenade : Projectile<Grenade>, IDamageable, ITransientSpawner, IChargeFlight
    {
        [Header("Charge")]
        [Stat, SerializeField, Min(0.01f)] private float launchSpeed = 40f;

        [Header("Fuse")]
        [Tooltip("Seconds after launch before arrival, contact or proximity can detonate the charge; being shot always does.")]
        [Stat, SerializeField, Min(0f)] internal float armingTime = 0.6f;
        [Tooltip("An armed charge detonates when any ship but its owner's comes within this distance.")]
        [Stat, SerializeField, Min(0f)] internal float proximityRadius = 4f;

        [Header("Blast")]
        [Stat, SerializeField] private ConcussionWave wavePrefab;

        private Vector3 targetPoint;
        private Vector3 heading;
        private bool detonated;
        private float sinceLaunch;
        private int shipLayers;

        private bool Armed => sinceLaunch >= armingTime;

        protected override DamageKind Kind => DamageKind.ConcussionWave;
        protected override bool TakesOwnerFire => true;

        public event Action<Vector3> OnDetonated;

        /// <summary>Announces the detonation's wave so whoever tracks this grenade tracks the wave too (<see cref="ITransientSpawner"/>).</summary>
        public event Action<MonoBehaviour, Action> Spawned;

        /// <summary>Where this charge comes to rest: the launch's target point, clamped to max range.</summary>
        public Vector3 TargetPoint => targetPoint;

        /// <summary>Serialized-state read for hangar stat lines (evaluated on the prefab asset).</summary>
        public ConcussionWave WavePrefab => wavePrefab;

        event Action IChargeFlight.Ended
        {
            add => ReturnedToPool += value;
            remove => ReturnedToPool -= value;
        }

        protected override void Awake()
        {
            base.Awake();
            shipLayers = LayerIds.Mask(LayerIds.Ship);
            SimplePool<ConcussionWave>.Warm(wavePrefab);
            DamageableColliders.Register(this, GetComponentsInChildren<Collider>(true));
        }

        private void OnDestroy() => DamageableColliders.Unregister(this);

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
            sinceLaunch += Time.fixedDeltaTime;
            var remaining = Vector3.Dot(targetPoint - transform.position, heading);
            var closingSpeed = Vector3.Dot(rb.linearVelocity, heading);
            var arrived = remaining <= 0f || closingSpeed <= 0f;
            if (Armed && (arrived || ShipInProximity()))
            {
                Detonate();
                return;
            }

            if (arrived)
                rb.linearVelocity = Vector3.zero;
            else
                rb.AddForce(-heading * BrakingDeceleration(closingSpeed, remaining, Time.fixedDeltaTime), ForceMode.Acceleration);
            base.FixedUpdate();
        }

        // Discrete-step form of v²/2d, which stops about v·dt/2 short; re-solved each step it holds constant.
        internal static float BrakingDeceleration(float speed, float distance, float dt) =>
            speed * speed / (2f * distance + speed * dt);

        private bool ShipInProximity()
        {
            var buffer = PhysicsBuffers.GetColliderBuffer();
            var hitCount = Physics.OverlapSphereNonAlloc(transform.position, proximityRadius, buffer, shipLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hitCount; i++)
                if (TryGetHitTarget(buffer[i], out _)) return true;
            return false;
        }

        protected override void OnHit(IDamageable other)
        {
            if (!Armed) return;
            RaiseHit(other);
            Detonate();
        }

        public float TakeDamage(in DamageInfo hit)
        {
            Detonate();
            return 0f;
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
            detonated = false;
            sinceLaunch = 0f;
        }
    }
}
