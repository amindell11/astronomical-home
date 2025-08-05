using UnityEngine;

namespace Asteroid
{
    public readonly struct AsteroidSpawnRequest
    {
        public enum SpawnKind { Random, Fragment }

        public readonly SpawnKind Kind;
        public readonly Pose Pose;
        public readonly float? Mass;
        public readonly Vector3? Velocity;
        public readonly Vector3? AngularVelocity;

        private AsteroidSpawnRequest(
            SpawnKind kind,
            Pose pose,
            float? mass,
            Vector3? velocity,
            Vector3? angularVelocity)
        {
            Kind = kind;
            Pose = pose;
            Mass = mass;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
        }

        // Factory for random spawn (random mass/scale computed by spawner)
        public static AsteroidSpawnRequest Random(Pose pose) =>
            new AsteroidSpawnRequest(SpawnKind.Random, pose, null, null, null);

        // Factory for fragment-driven spawn (all physics pre-computed by fragmenter)
        public static AsteroidSpawnRequest Fragment(
            Pose pose,
            float mass,
            Vector3 velocity,
            Vector3 angularVelocity) =>
            new AsteroidSpawnRequest(SpawnKind.Fragment, pose, mass, velocity, angularVelocity);
    }
} 