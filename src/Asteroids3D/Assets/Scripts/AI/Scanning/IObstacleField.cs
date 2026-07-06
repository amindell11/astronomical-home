using UnityEngine;
namespace AI.Scanning
{
    /// <summary>Queries live asteroids in a fixed AABB around a point (plane coords).</summary>
    public interface IObstacleField
    {
        int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer);
    }
    /// <summary>Supplies the currently active obstacle field (may be null between sectors).</summary>
    public interface IObstacleFieldProvider
    {
        IObstacleField ObstacleField { get; }
    }
}
