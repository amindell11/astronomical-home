using Asteroids.Fragnetics;
using Damage;
using UnityEngine;
using Utils;

namespace Asteroids
{
    [RequireComponent(typeof(AsteroidController))]
    public sealed class AsteroidDamage : MonoBehaviour, IDamageable
    {
        [Header("Damage Tuning")]
        [SerializeField]
        [Tooltip("Damage per unit of ship velocity change (delta-v) from the collision.")]
        private float damagePerDeltaV = 1.5f;

        [SerializeField]
        [Tooltip("Delta-v at or below this is a free tap: no damage.")]
        private float graceDeltaV = 2f;

        [Header("Health")]
        [SerializeField]
        [Tooltip("Base health per unit volume. Total health = volume * this value.")]
        private float healthPerUnitVolume = 10f;

        private AsteroidController controller;
        private bool isDestroyed;
        private float lethality = 1f;

        public float Health { get; private set; }
        public float MaxHealth { get; private set; }

        private void Awake()
        {
            controller = GetComponent<AsteroidController>();
        }

        public void Initialize(float volume, float lethality = 1f)
        {
            isDestroyed = false;
            this.lethality = lethality;
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

        public void TakeDamage(in DamageInfo hit)
        {
            if (!controller) return;
            if (isDestroyed) return;

            Health -= hit.Amount;
            if (Health > 0f) return;

            isDestroyed = true;
            controller.HandleDestroyed(new HitData(hit.HitMass, hit.HitVelocity, hit.HitPoint));
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
            var damage = CalcDamage(deltaV);
            if (damage <= 0f) return;

            var damageable = collision.gameObject.GetComponent<IDamageable>();
            damageable?.TakeDamage(new DamageInfo(damage, DamageKind.Collision, Ships.Registry.ShipId.Invalid,
                controller.Mass, controller.Rb.linearVelocity, collision.GetContact(0).point));
        }

        internal float CalcDamage(float deltaV)
        {
            return Mathf.Max(0f, deltaV - graceDeltaV) * damagePerDeltaV * lethality;
        }
    }
}
