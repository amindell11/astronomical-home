using UnityEngine;

namespace RL.Opponents
{
    /// <summary>The pure roam laws a live-targeting pilot flies with its target gone: held waypoints on a loose ring around the wreck or in a wander disc over it, and the seek toward the held one.</summary>
    internal static class ArchetypeRoam
    {
        internal const float StepMinDegrees = 60f;
        internal const float StepMaxDegrees = 100f;
        internal const float ArrivalRadius = 15f;

        internal static Vector2 RingWaypoint(Vector2 center, ref float angleDegrees, int lane, float radiusMin,
            float radiusMax, System.Random rng, out float stepDegrees)
        {
            stepDegrees = Draw(rng, StepMinDegrees, StepMaxDegrees);
            angleDegrees += lane * stepDegrees;
            return center + Draw(rng, radiusMin, radiusMax) * Direction(angleDegrees * Mathf.Deg2Rad);
        }

        internal static Vector2 WanderWaypoint(Vector2 center, float radius, System.Random rng)
        {
            var angle = Draw(rng, 0f, 2f * Mathf.PI);
            // sqrt spreads the draw uniformly over the disc's area instead of bunching it at the centre.
            return center + Mathf.Sqrt((float)rng.NextDouble()) * radius * Direction(angle);
        }

        internal static Vector2 SeekVelocity(Vector2 pos, Vector2 waypoint, float speed) =>
            (waypoint - pos).normalized * speed;

        private static Vector2 Direction(float radians) => new(Mathf.Cos(radians), Mathf.Sin(radians));

        private static float Draw(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);
    }
}
