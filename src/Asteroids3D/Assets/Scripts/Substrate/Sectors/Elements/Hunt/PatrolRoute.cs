using UnityEngine;

namespace Substrate.Sectors.Elements.Hunt
{
    internal static class PatrolRoute
    {
        /// <summary>The looped waypoints: beside the start, the sector center, back to the spawn point. The pass point sits square to the patrol's approach, so patrols on different bearings pass on different sides.</summary>
        public static Vector2[] Waypoints(Vector2 start, Vector2 center, Vector2 spawn, float startPass)
        {
            var approach = (start - spawn).normalized;
            var beside = new Vector2(-approach.y, approach.x);
            return new[] { start + beside * startPass, center, spawn };
        }
    }
}
