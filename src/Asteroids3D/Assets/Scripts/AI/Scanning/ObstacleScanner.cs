using Game;
using UnityEngine;

namespace AI.Scanning
{
    public readonly struct DetectedObstacle
    {
        public readonly Vector2 position;
        public readonly float radius;
        public readonly Collider collider;

        public DetectedObstacle(Collider collider)
        {
            this.collider = collider;
            position = GamePlane.WorldPointToPlane(collider.transform.position);
            radius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
        }

        // Used for non-static obstacles (e.g. other ships) where the relevant world
        // position is the ship's transform, not necessarily a child collider's local origin,
        // and radius comes from ship settings rather than collider bounds.
        public DetectedObstacle(Vector3 worldPos, float radius, Collider collider)
        {
            this.collider = collider;
            position = GamePlane.WorldPointToPlane(worldPos);
            this.radius = radius;
        }
    }

    public readonly struct ObstacleScan
    {
        public readonly DetectedObstacle[] buffer;
        public readonly int count;

        public ObstacleScan(DetectedObstacle[] buffer, int count)
        {
            this.buffer = buffer;
            this.count = count;
        }

        public static implicit operator (DetectedObstacle[] buffer, int count)(ObstacleScan scan) => (scan.buffer, scan.count);
        public static implicit operator ObstacleScan((DetectedObstacle[] buffer, int count) tuple) => new ObstacleScan(tuple.buffer, tuple.count);
    }

    /// <summary>
    /// Fills a buffer with live asteroids inside a fixed-size AABB around the ship by
    /// querying the deterministic asteroid field directly (no physics overlap). Destroyed
    /// asteroids are never reported. The MPC handles relevance through its cost function.
    /// </summary>
    public class ObstacleScanner
    {
        private readonly Transform origin;

        public DetectedObstacle[] DetectedBuffer { get; }
        public int DetectedCount { get; private set; }

        /// <summary>Half-extent (per axis) of the fixed query box, in plane units.</summary>
        public float HalfExtent { get; set; }

        public ObstacleScanner(Transform origin, int bufferSize = 64)
        {
            this.origin = origin;
            DetectedBuffer = new DetectedObstacle[bufferSize];
            DetectedCount = 0;
        }

        /// <summary>
        /// Query the supplied field for live asteroids inside the fixed box around the ship.
        /// A null field (no active sector) clears the buffer.
        /// </summary>
        public void Scan(IObstacleField field)
        {
            DetectedCount = 0;
            if (field == null) return;
            var centerPlane = GamePlane.WorldPointToPlane(origin.position);
            DetectedCount = field.QueryObstacles(centerPlane, HalfExtent, DetectedBuffer);
        }
    }
}
