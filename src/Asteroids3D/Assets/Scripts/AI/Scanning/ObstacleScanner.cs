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

        /// <summary>
        /// Half-extent (per axis) of the fixed query box, in plane units: the worst-case
        /// travel envelope over the lookahead horizon at max speed — a per-ship constant
        /// computed once at construction, so the obstacle set never breathes with speed.
        /// </summary>
        public float HalfExtent { get; }

        /// <param name="maxSpeed">Ship max speed (plane units/s).</param>
        /// <param name="maxAccel">Max acceleration magnitude (units/s²); extends the envelope.</param>
        /// <param name="lookaheadTime">Planning horizon the envelope must cover, seconds.</param>
        public ObstacleScanner(Transform origin, float maxSpeed, float maxAccel,
            float lookaheadTime, int bufferSize = 64)
        {
            this.origin = origin;
            HalfExtent = maxSpeed * lookaheadTime + 0.5f * maxAccel * lookaheadTime * lookaheadTime;
            DetectedBuffer = new DetectedObstacle[bufferSize];
            DetectedCount = 0;
        }

        /// <summary>
        /// Query the session's active obstacle field (<see cref="ObstacleFields.Active"/>)
        /// for live asteroids inside the fixed box around the ship. No active field
        /// (no sector, or a sector without asteroids) clears the buffer.
        /// </summary>
        public void Scan()
        {
            DetectedCount = 0;
            var field = ObstacleFields.Active;
            if (field == null) return;
            var centerPlane = GamePlane.WorldPointToPlane(origin.position);
            DetectedCount = field.QueryObstacles(centerPlane, HalfExtent, DetectedBuffer);
        }
    }
}
