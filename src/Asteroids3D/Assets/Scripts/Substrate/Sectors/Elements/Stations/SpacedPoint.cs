using System.Collections.Generic;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    public static class SpacedPoint
    {
        /// <summary>
        /// A random plane point between the two distances from <paramref name="center"/> with none of
        /// <paramref name="spacedFrom"/> within <paramref name="spacing"/>; the last point tried when none of the
        /// attempts is spaced.
        /// </summary>
        public static Vector3 Around(Vector2 center, float minDistance, float maxDistance, int attempts,
            IReadOnlyList<Vector3> spacedFrom, float spacing)
        {
            var position = Vector3.zero;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var angle = Random.Range(0f, 2f * Mathf.PI);
                var distance = Random.Range(minDistance, maxDistance);
                position = GamePlane.PlanePointToWorld(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
                if (Spaced(position, spacedFrom, spacing)) break;
            }
            return position;
        }

        private static bool Spaced(Vector3 point, IReadOnlyList<Vector3> others, float spacing)
        {
            foreach (var other in others)
                if ((point - other).sqrMagnitude < spacing * spacing) return false;
            return true;
        }
    }
}
