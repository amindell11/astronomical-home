using UnityEngine;
namespace AI.Scanning
{
    /// <summary>Queries live asteroids in a fixed AABB around a point (plane coords).</summary>
    public interface IObstacleField
    {
        int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer);
    }

    /// <summary>
    /// INTERIM access point for the session's active obstacle field: the owning lifecycle
    /// (the sector's <c>AsteroidFieldSpawner</c>) registers on build and unregisters on
    /// teardown; consumers (obstacle scan, terminal nav field) pull <see cref="Active"/>
    /// directly. Null means "no field": ships sense zero static obstacles.
    /// <para>
    /// DEFERRED DESIGN: a process-wide static assumes one field per process, which breaks
    /// once multiple arena instances run in one process (planned for RL training). How
    /// world-scoped state should reach consumers in that world is deliberately an open
    /// question — keep this surface minimal and don't copy the pattern to new systems
    /// without revisiting it.
    /// </para>
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
