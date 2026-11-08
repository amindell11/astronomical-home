using UnityEngine;

namespace Substrate.Sectors.Elements.Hunt
{
    internal static class PatrolRoute
    {
        public static Vector2[] Waypoints(Vector2 start, Vector2 center, Vector2 spawn, float startPass)
        {
            var approach = (start - spawn).normalized;
            // Square to the approach, so patrols on different bearings pass on different sides.
            var beside = new Vector2(-approach.y, approach.x);
            return new[] { start + beside * startPass, center, spawn };
        }
    }
}
