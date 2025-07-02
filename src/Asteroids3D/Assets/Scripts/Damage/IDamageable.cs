using UnityEngine;

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
    /// <param name="projectileMass">Mass of the projectile causing damage</param>
    /// <param name="projectileVelocity">Velocity of the projectile causing damage</param>
    /// <param name="hitPoint">World position where the damage occurred</param>
    /// <param name="attacker">The GameObject that caused the damage</param>
    void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint, GameObject attacker);
}