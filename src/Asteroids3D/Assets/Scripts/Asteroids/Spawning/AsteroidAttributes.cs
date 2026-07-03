using UnityEngine;

namespace Asteroids.Spawning
{
    /// <summary>
    /// Everything that defines an asteroid's identity apart from its pose:
    /// mesh, mass, scale and initial kinematics. Orientation travels on the
    /// spawn <see cref="Pose"/>; the deterministic field generates both from
    /// one seeded stream (<see cref="Fields.Core.FieldAsteroidSpec"/>).
    /// </summary>
    public readonly struct AsteroidAttributes
    {
        public readonly AsteroidSpawnSettings.MeshInfo MeshInfo;
        public readonly float Mass;
        public readonly float Scale;
        public readonly Vector3 Velocity;
        public readonly Vector3 AngularVelocity;

        public AsteroidAttributes(
            AsteroidSpawnSettings.MeshInfo meshInfo,
            float mass,
            float scale,
            Vector3 velocity,
            Vector3 angularVelocity)
        {
            MeshInfo = meshInfo;
            Mass = mass;
            Scale = scale;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
        }
    }

}
