using System;
using Asteroids.Fragnetics;
using Damage;
using Ships;
using UnityEngine;
using Substrate;

namespace Asteroids
{
    [RequireComponent(typeof(AsteroidController))]
    public sealed class AsteroidDamage : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField]
        [Tooltip("Base health per unit volume. Total health = volume * this value.")]
        private float healthPerUnitVolume = 10f;

        private AsteroidController controller;
        private bool isDestroyed;
        private CollisionDamageLaw law;
        private Ship thrower;

        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public event Action<Vector3> OnHit;
        public event Action Thrown;

        private void Awake()
        {
            controller = GetComponent<AsteroidController>();
            DamageableColliders.Register(this, GetComponents<MeshCollider>());
        }

        private void OnDestroy() => DamageableColliders.Unregister(this);

        public void Initialize(float volume, CollisionDamageLaw law)
        {
            isDestroyed = false;
            this.law = law;
            thrower = null;
            MaxHealth = volume * healthPerUnitVolume;
            Health = MaxHealth;
        }

        public void ResetDamage(float volume)
        {
            isDestroyed = false;
            MaxHealth = volume * healthPerUnitVolume;
            Health = MaxHealth;
        }

        /// <summary>
        /// Restores persisted damage on reload: sets remaining health to the
        /// given fraction of max (deterministic field override overlay).
        /// </summary>
        public void ApplyHealthFraction(float fraction)
        {
            Health = MaxHealth * Mathf.Clamp01(fraction);
        }

        public float TakeDamage(in DamageInfo hit)
        {
            if (!controller) return 0f;
            // Already broken up: a later hit in the same step passes straight through.
            if (isDestroyed) return hit.Amount;

            OnHit?.Invoke(hit.HitPoint);
            Health -= hit.Amount;
            if (Health > 0f) return 0f;

            isDestroyed = true;
            var shrapnel = hit.Attacker && hit.Attacker.ShrapnelRocks.Held ? hit.Attacker : null;
            controller.HandleDestroyed(new HitData(hit.HitMass, hit.HitVelocity, hit.HitPoint), shrapnel);
            return -Health;
        }

        /// <summary>Marks this fragment as thrown by <paramref name="ship"/>'s Shrapnel rocks.</summary>
        public void ThrownBy(Ship ship)
        {
            thrower = ship;
            Thrown?.Invoke();
        }

        public void HandleCollision(Collision collision)
        {
            if (!controller) return;
            if (collision.gameObject.layer != LayerIds.Ship) return;

            var otherRb = collision.rigidbody;
            if (!otherRb) return;

            // Solver impulse / ship mass = the delta-v the ship actually felt: damage
            // tracks the knock the player experienced, whatever the contact geometry.
            var deltaV = collision.impulse.magnitude / otherRb.mass;
            if (!DamageableColliders.TryGet(collision.collider, out var damageable)) return;
            var damage = DamageTo(damageable, deltaV);
            if (damage <= 0f) return;

            damageable.TakeDamage(new DamageInfo(damage, DamageKind.Collision, Ships.Registry.ShipId.Invalid,
                controller.Mass, controller.Rb.linearVelocity, collision.GetContact(0).point));
        }

        internal float CalcDamage(float deltaV) => law.Damage(deltaV);

        internal float DamageTo(IDamageable struck, float deltaV) =>
            thrower && struck != (IDamageable)thrower.Damage
                ? CalcDamage(deltaV) * thrower.ShrapnelRocks.EnemyDamageScale
                : CalcDamage(deltaV);
    }
}
