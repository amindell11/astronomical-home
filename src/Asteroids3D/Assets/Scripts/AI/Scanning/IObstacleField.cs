using UnityEngine;
namespace AI.Scanning
{
    /// <summary>Queries live asteroids around a point (plane coords).</summary>
    public interface IObstacleField
    {
        int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer);

        /// <summary>True when any rock's surface lies within <paramref name="radius"/> of the point.</summary>
        bool AnyObstacleWithin(Vector2 centerPlane, float radius);
    }
}
