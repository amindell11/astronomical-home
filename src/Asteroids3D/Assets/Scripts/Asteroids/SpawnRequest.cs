using Asteroids.Fragnetics;
using UnityEngine;

namespace Asteroids
{
    public readonly struct SpawnRequest
    {
        public enum SpawnKind { Random, Fragment }

        public readonly SpawnKind Kind;
        public readonly Pose Pose;
        public readonly float? Mass;
        public readonly Vector3? Velocity;
        public readonly Vector3? AngularVelocity;

        private SpawnRequest(
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
        public static SpawnRequest Random(Pose pose) =>
            new SpawnRequest(SpawnKind.Random, pose, null, null, null);

        // Factory for fragment-driven spawn (all physics pre-computed by fragmenter)
        public static SpawnRequest Fragment(Frag frag) =>
            new SpawnRequest(SpawnKind.Fragment, new Pose(frag.Position, frag.Rotation), frag.Mass, frag.Velocity, frag.Spin);
    }
} 