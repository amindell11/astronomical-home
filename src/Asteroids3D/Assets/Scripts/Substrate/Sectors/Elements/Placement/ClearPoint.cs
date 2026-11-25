using AI.Scanning;
using UnityEngine;

namespace Substrate.Sectors.Elements.Placement
{
    public static class ClearPoint
    {
        public static bool IsClear(IObstacleField field, Vector2 planePoint, float clearance) =>
            field == null || !field.AnyObstacleWithin(planePoint, clearance);

        /// <summary>
        /// A random plane point between the two distances from <paramref name="center"/> with no asteroid within
        /// <paramref name="clearance"/>; the last point tried when none of the attempts is clear.
        /// </summary>
        public static Vector3 Around(Vector2 center, float minDistance, float maxDistance, float clearance,
            IObstacleField field, int attempts)
        {
            var position = Vector3.zero;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var angle = Random.Range(0f, 2f * Mathf.PI);
                var distance = Random.Range(minDistance, maxDistance);
                var planePoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                position = GamePlane.PlanePointToWorld(planePoint);
                if (IsClear(field, planePoint, clearance)) break;
            }
            return position;
        }
    }
}
