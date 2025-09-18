using UnityEngine;

namespace Damage
{
    public interface IDamageable
    {
        /// <summary>
        /// The GameObject this IDamageable component is attached to
        /// </summary>
        GameObject gameObject { get; }

        /// <summary>
        /// Apply damage to this object
        /// </summary>
        /// <param name="damage">Amount of damage to apply</param>
        /// <param name="hitMass">Mass of the projectile causing damage</param>
        /// <param name="hitVelocity">Velocity of the projectile causing damage</param>
        /// <param name="hitPoint">World position where the damage occurred</param>
        /// <param name="attacker">The GameObject that caused the damage</param>
        void TakeDamage(float damage, float hitMass, Vector3 hitVelocity, Vector3 hitPoint, GameObject attacker);
    }
}