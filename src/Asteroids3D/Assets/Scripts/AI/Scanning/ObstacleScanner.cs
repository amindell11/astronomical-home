using System.Collections.Generic;
using Asteroids;
using Asteroids.Fields;
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

        // Used when the relevant world position is not the collider transform.
        public DetectedObstacle(Vector3 worldPos, float radius, Collider collider)
        {
            this.collider = collider;
            position = GamePlane.WorldPointToPlane(worldPos);
            this.radius = radius;
        }

        public DetectedObstacle(Vector2 planePosition, float radius, Collider collider)
        {
            this.collider = collider;
            position = planePosition;
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
    /// Detects static asteroid obstacles through the deterministic field's live
    /// registry in a fixed AABB. The MPC still decides relevance through cost.
    /// </summary>
    public class ObstacleScanner
    {
        private static readonly Collider[] ScratchBuffer = new Collider[128];

        private readonly Transform origin;
        private readonly LayerMask obstacleMask;
        private readonly GameObject selfRoot;
        private readonly List<LiveAsteroidQueryHit> fieldHits = new(128);
        private Transform excludeRoot;

        public DetectedObstacle[] DetectedBuffer { get; }
        public int DetectedCount { get; private set; }

        /// <summary>Effective fixed radius used in the last AABB scan.</summary>
        public float Radius { get; private set; }

        /// <summary>Max acceleration magnitude. Used to compute worst-case stopping distance.</summary>
        public float MaxAccel { get; set; }

        public ObstacleScanner(Transform origin, LayerMask obstacleMask, int bufferSize = 64)
        {
            this.origin = origin;
            this.obstacleMask = obstacleMask;
            selfRoot = origin.gameObject;
            DetectedBuffer = new DetectedObstacle[bufferSize];
            DetectedCount = 0;
        }

        public void SetExcludeRoot(Transform root) => excludeRoot = root;
        public void ClearExcludeRoot() => excludeRoot = null;

        public void Scan(float maxSpeed)
        {
            Radius = maxSpeed + (MaxAccel > 0f ? (maxSpeed * maxSpeed) / (2f * MaxAccel) : 0f);
            DetectedCount = 0;

            var center = GamePlane.WorldPointToPlane(origin.position);
            var halfExtents = Vector2.one * Radius;
            AsteroidFieldRegistry.QueryLiveAsteroidsAabb(center, halfExtents, fieldHits);
            for (var i = 0; i < fieldHits.Count && DetectedCount < DetectedBuffer.Length; i++)
            {
                var hit = fieldHits[i];
                var col = hit.collider;
                if (col && col.gameObject != selfRoot && col.transform.root != origin.root
                    && (!excludeRoot || col.transform.root != excludeRoot))
                {
                    DetectedBuffer[DetectedCount++] = new DetectedObstacle(hit.planePosition, hit.radius, col);
                }
            }

            // Fixed-radius fallback for authored non-field obstacles in tests/tools.
            var count = Physics.OverlapSphereNonAlloc(
                origin.position, Radius, ScratchBuffer, obstacleMask,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count && DetectedCount < DetectedBuffer.Length; i++)
            {
                var col = ScratchBuffer[i];
                if (!col || col.GetComponentInParent<AsteroidController>()) continue;
                if (col.gameObject != selfRoot && col.transform.root != origin.root
                    && (!excludeRoot || col.transform.root != excludeRoot))
                {
                    DetectedBuffer[DetectedCount++] = new DetectedObstacle(col);
                }
            }
        }
    }
}
