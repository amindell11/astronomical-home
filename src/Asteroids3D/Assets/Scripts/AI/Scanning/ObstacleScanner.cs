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
    /// Detects obstacles within a single OverlapSphere whose radius expands with speed,
    /// letting the MPC handle relevance through its cost function.
    /// </summary>
    public class ObstacleScanner
    {
        private static readonly Collider[] ScratchBuffer = new Collider[128];

        private readonly Transform origin;
        private readonly LayerMask obstacleMask;
        private readonly GameObject selfRoot;
        private Transform excludeRoot;

        public DetectedObstacle[] DetectedBuffer { get; }
        public int DetectedCount { get; private set; }

        /// <summary>Effective radius used in the last scan.</summary>
        public float Radius { get; private set; }

        /// <summary>Lookahead time in seconds.</summary>
        public float LookaheadTime { get; set; }

        /// <summary>Max acceleration magnitude (m/s²). Used to extend detection range.</summary>
        public float MaxAccel { get; set; }

        public ObstacleScanner(Transform origin, LayerMask obstacleMask, float lookaheadTime = 2f, int bufferSize = 64)
        {
            this.origin = origin;
            this.obstacleMask = obstacleMask;
            selfRoot = origin.gameObject;
            LookaheadTime = lookaheadTime;
            DetectedBuffer = new DetectedObstacle[bufferSize];
            DetectedCount = 0;
        }

        public void SetExcludeRoot(Transform root) => excludeRoot = root;
        public void ClearExcludeRoot() => excludeRoot = null;

        /// <summary>
        /// Scan for obstacles. Radius covers the distance reachable within
        /// the lookahead time from current speed plus max-acceleration contribution.
        /// </summary>
        public void Scan(Vector2 vel, float maxSpeed)
        {
            var t = LookaheadTime;
            Radius = vel.magnitude * t + 0.5f * MaxAccel * t * t;
            var count = Physics.OverlapSphereNonAlloc(
                origin.position, Radius, ScratchBuffer, obstacleMask,
                QueryTriggerInteraction.Ignore);

            DetectedCount = 0;
            for (var i = 0; i < count && DetectedCount < DetectedBuffer.Length; i++)
            {
                var col = ScratchBuffer[i];
                if (col && col.gameObject != selfRoot && col.transform.root != origin.root
                    && (!excludeRoot || col.transform.root != excludeRoot))
                    DetectedBuffer[DetectedCount++] = new DetectedObstacle(col);
            }
        }
    }
}
