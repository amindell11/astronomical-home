using UnityEngine;
namespace AI.Scanning
{
    /// <summary>Queries live asteroids in a fixed AABB around a point (plane coords).</summary>
    public interface IObstacleField
    {
        int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer);

        /// <summary>Every obstacle in the box, into a caller-owned buffer the producer grows as needed.</summary>
        int QueryAllObstacles(Vector2 centerPlane, float halfExtent, ref DetectedObstacle[] buffer);
    }
}
