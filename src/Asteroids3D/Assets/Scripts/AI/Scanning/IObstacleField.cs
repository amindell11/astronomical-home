using UnityEngine;
namespace AI.Scanning
{
    /// <summary>Queries live asteroids in a fixed AABB around a point (plane coords).</summary>
    public interface IObstacleField
    {
        int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer);
    }

    /// <summary>
    /// Static access point for the session's active obstacle field, following the
    /// <see cref="Game.GamePlane"/> pattern for world-scoped state: the owning lifecycle
    /// (the sector's <c>AsteroidFieldSpawner</c>) registers on build and unregisters on
    /// teardown; consumers (obstacle scan, terminal nav field) pull <see cref="Active"/>
    /// directly. World state is never threaded through per-ship wiring — AI components
    /// take per-ship dependencies once via Initialize and read world state from access
    /// points like this one. Null means "no field": ships sense zero static obstacles.
    /// </summary>
    public static class ObstacleFields
    {
        public static IObstacleField Active { get; private set; }

        public static void Register(IObstacleField field) => Active = field;

        /// <summary>Clears <see cref="Active"/> only if it is still this field, so a stale
        /// teardown can never clobber a newer sector's registration.</summary>
        public static void Unregister(IObstacleField field)
        {
            if (ReferenceEquals(Active, field)) Active = null;
        }
    }
}
