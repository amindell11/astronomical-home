using UnityEngine;

namespace Damage
{
    /// <summary>
    /// Utility methods for converting physical collision data (mass & relative velocity)
    /// into abstract damage numbers used by gameplay systems.
    /// </summary>
    public static class CollisionDamageUtility
    {
        public static float KineticEnergy(float mass, Vector3 relativeVelocity)
        {
            return 0.5f * mass * relativeVelocity.sqrMagnitude;
        }

        public static float ComputeDamage(float mass, Vector3 relativeVelocity, float energyToDamageScale)
        {
            var energy = KineticEnergy(mass, relativeVelocity);
            return energy * energyToDamageScale;
        }
        public static float RelativeKineticEnergy(float massA, Vector3 velocityA, float massB, Vector3 velocityB)
        {
            var vRel = velocityA - velocityB;
            var   reducedMass = (massA + massB) > 0f ? (massA * massB) / (massA + massB) : 0f;
            return 0.5f * reducedMass * vRel.sqrMagnitude;
        }
        public static float ComputeDamage(
            float massA,
            Vector3 velocityA,
            float massB,
            Vector3 velocityB,
            float energyToDamageScale)
        {
            var energy = RelativeKineticEnergy(massA, velocityA, massB, velocityB);
            return energy * energyToDamageScale;
        }
    }
} 