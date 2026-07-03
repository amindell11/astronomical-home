using UnityEngine;

namespace Asteroids.Spawning
{
    /// <summary>
    /// Everything that defines an asteroid's identity apart from its pose:
    /// mesh, mass, scale and initial kinematics. Orientation stays on the
    /// spawn <see cref="Pose"/> for now; the deterministic provider (PR2)
    /// will generate the full pose alongside these attributes.
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

    /// <summary>
    /// Seam between "decide what an asteroid is" and "put it in the world"
    /// (<see cref="AsteroidSpawner.Spawn"/>). The deterministic seeded
    /// provider (PR2) replaces <see cref="RandomAsteroidAttributeRoller"/>
    /// behind this interface.
    /// </summary>
    public interface IAsteroidAttributeProvider
    {
        AsteroidAttributes Roll();
    }
}
