using UnityEngine;

public interface IDamageable
{
    /// <summary>
    /// Apply damage to this object
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    /// <param name="projectileMass">Mass of the projectile causing damage</param>
    /// <param name="projectileVelocity">Velocity of the projectile causing damage</param>
    /// <param name="hitPoint">World position where the damage occurred</param>
    void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint);
} 