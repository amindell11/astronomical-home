using UnityEngine;

namespace Asteroids.Fragnetics
{
    public struct Result
    {
        public readonly Vector3[] Velocities;
        public readonly Vector3[] Spins;
        public Result(Vector3[] velocities, Vector3[] spins)
        {
            Velocities = velocities;
            Spins = spins;
        }
    }
        
    public readonly struct FragmentSpecification
    {
        public readonly float[] Masses;
        public readonly Vector3[] Positions;
        public int Count => Masses?.Length ?? 0;

        public FragmentSpecification(float[] masses, Vector3[] positions)
        {
            Masses = masses;
            Positions = positions;
        }
    }

    public readonly struct ProjectileData
    {
        public readonly float ProjectileMass;
        public readonly Vector3 ProjectileVelocity;
        public readonly Vector3 HitPoint;

        public ProjectileData(float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint)
        {
            HitPoint = hitPoint;
            ProjectileMass = projectileMass;
            ProjectileVelocity = projectileVelocity;
        }
    }
}