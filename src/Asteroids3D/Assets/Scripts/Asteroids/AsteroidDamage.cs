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
        [Tooltip("Multiplier that converts collision energy (J) to gameplay damage.")]
        private float energyToDamageScale = 0.01f;

        [Header("Damage Soft Cap")]
        [SerializeField, Tooltip("Damage below this value is unaffected; excess is softened")]
        private float softCapThreshold = 50f;

        [SerializeField, Range(0.1f, 1f), Tooltip("Exponent applied to damage above the soft-cap threshold (0.5 = sqrt, 1 = no cap)")]
        private float softCapExponent = 0.5f;

        [Header("Health")]
        [SerializeField]
        [Tooltip("Base health per unit volume. Total health = volume * this value.")]
        private float healthPerUnitVolume = 10f;

        private AsteroidController controller;
        private bool isDestroyed;

        public float Health { get; private set; }
        public float MaxHealth { get; private set; }

        private void Awake()
        {
            controller = GetComponent<AsteroidController>();
        }

        public void Initialize(float volume)
        {
            isDestroyed = false;
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

        public void TakeDamage(float damage, float hitMass, Vector3 hitVelocity, Vector3 hitPoint, GameObject attacker)
        {
            if (!controller) return;
            if (isDestroyed) return;

            Health -= damage;
            if (Health > 0f) return;

            isDestroyed = true;
            controller.HandleDestroyed(new HitData(hitMass, hitVelocity, hitPoint));
        }

        public void HandleCollision(Collision collision)
        {
            if (!controller) return;
            if (collision.gameObject.layer != LayerIds.Ship) return;

            var otherRb = collision.rigidbody;
            if (!otherRb) return;

            var impact = collision.GetContact(0);
            var damage = CalcDamage(otherRb.mass, otherRb.linearVelocity, impact);
            var damageable = collision.gameObject.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage, controller.Mass, controller.Rb.linearVelocity, impact.point, gameObject);
        }

        private float CalcDamage(float shipMass, Vector3 shipVel, ContactPoint impact)
        {
            var asteroidNormalVelocity = Vector3.Project(controller.Rb.linearVelocity, impact.normal);
            var shipNormalVelocity = Vector3.Project(shipVel, impact.normal);
            var damage = CollisionDamageUtility.ComputeDamage(
                controller.Mass,
                asteroidNormalVelocity,
                shipMass,
                shipNormalVelocity,
                energyToDamageScale);
            return ApplySoftCap(damage);
        }

        private float ApplySoftCap(float damage)
        {
            if (damage <= softCapThreshold) return damage;
            var excess = damage - softCapThreshold;
            return softCapThreshold + Mathf.Pow(excess, softCapExponent);
        }
    }
}
