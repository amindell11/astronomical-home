using UnityEngine;

/// <summary>
/// Utility methods for converting physical collision data (mass & relative velocity)
/// into abstract damage numbers used by gameplay systems.
/// </summary>
public static class CollisionDamageUtility
{
    /// <summary>
    /// Computes kinetic energy (&frac12; m v²) in joules.
    /// </summary>
    /// <param name="mass">Combined or impacting mass in kilograms.</param>
    /// <param name="relativeVelocity">Relative velocity between the two colliders.</param>
    /// <returns>Energy in joules.</returns>
    public static float KineticEnergy(float mass, Vector3 relativeVelocity)
    {
        // &frac12; m v² – we use sqrMagnitude to avoid the expensive sqrt.
        return 0.5f * mass * relativeVelocity.sqrMagnitude;
    }

    /// <summary>
    /// Converts kinetic energy to a damage value using a tunable scale factor.
    /// </summary>
    /// <param name="mass">Combined or impacting mass in kilograms.</param>
    /// <param name="relativeVelocity">Relative velocity between the two colliders.</param>
    /// <param name="energyToDamageScale">Experimental tuneable factor – e.g. 0.01f.</param>
    /// <returns>Damage amount to feed into <see cref="IDamageable.TakeDamage(float, float, Vector3, Vector3)"/>.</returns>
    public static float ComputeDamage(float mass, Vector3 relativeVelocity, float energyToDamageScale)
    {
        float energy = KineticEnergy(mass, relativeVelocity);
        return energy * energyToDamageScale;
    }

    /// <summary>
    /// Computes the relative kinetic energy for two colliding bodies using the
    /// reduced mass μ = (m₁·m₂)/(m₁+m₂) and the relative velocity |v₁–v₂|.
    /// </summary>
    /// <remarks>
    /// This is the amount of energy available to be converted into damage in the
    /// centre-of-mass frame (perfectly inelastic assumption).
    /// </remarks>
    /// <param name="massA">Mass of the first body.</param>
    /// <param name="velocityA">Velocity of the first body.</param>
    /// <param name="massB">Mass of the second body.</param>
    /// <param name="velocityB">Velocity of the second body.</param>
    /// <returns>Kinetic energy in joules available in the collision.</returns>
    public static float RelativeKineticEnergy(float massA, Vector3 velocityA, float massB, Vector3 velocityB)
    {
        Vector3 vRel = velocityA - velocityB;
        float   reducedMass = (massA + massB) > 0f ? (massA * massB) / (massA + massB) : 0f;
        return 0.5f * reducedMass * vRel.sqrMagnitude;
    }

    /// <summary>
    /// Convenience method: converts collision energy between two bodies directly
    /// to a damage value using a scale factor.
    /// </summary>
    public static float ComputeDamage(
        float massA,
        Vector3 velocityA,
        float massB,
        Vector3 velocityB,
        float energyToDamageScale)
    {
        float energy = RelativeKineticEnergy(massA, velocityA, massB, velocityB);
        return energy * energyToDamageScale;
    }

    // Preserve single-body overload for projectiles etc.
} 