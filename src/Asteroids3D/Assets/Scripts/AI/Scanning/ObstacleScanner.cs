using System.Collections.Generic;
using System.Linq;
using AI.Scanning.Sensors;
using Game;
using UnityEngine;

namespace AI.Scanning
{
    public readonly struct DetectedObstacle
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly Collider Collider;

        public DetectedObstacle(Collider collider)
        {
            Collider = collider;
            Position = GamePlane.WorldPointToPlane(collider.transform.position);
            Radius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
        }
    }

    public readonly struct ObstacleScanResult
    {
        public readonly int hitCount;
        public readonly Collider[] Obstacles;

        public ObstacleScanResult(Collider[] buffer, int count)
        {
            Obstacles = buffer;
            hitCount = count;
        }
    }

    public partial class ObstacleScanner
    {
        private readonly RayFanSensor sensor;
        private readonly float scanDistance;
        private readonly DetectedObstacle[] detectedBuffer;
        private int detectedCount;
        private ObstacleScanResult lastResult;

        public ObstacleScanResult LastResult => lastResult;
        public DetectedObstacle[] DetectedBuffer => detectedBuffer;
        public int DetectedCount => detectedCount;

        public ObstacleScanner(Transform origin, float distance, LayerMask obstacleMask, int raysPerSide = 5, float spreadAngle = 90f, float sphereRadius = 0.5f, int bufferSize = 64)
        {
            scanDistance = distance;
            sensor = new RayFanSensor(origin, distance, obstacleMask, raysPerSide, spreadAngle, sphereRadius, bufferSize);
            detectedBuffer = new DetectedObstacle[bufferSize];
            detectedCount = 0;
            lastResult = new ObstacleScanResult(sensor.Buffer, 0);
        }

        public ObstacleScanResult Scan(Vector2 scanDir)
        {
            ClearDebugRays();
            
            var direction = GamePlane.PlaneDirToWorld(scanDir).normalized;
            var count = sensor.Detect(direction);
            
            for (var i = 0; i < sensor.DirectionCount; i++)
            {
                AddDebugRay(sensor.Directions[i] * scanDistance);
            }
            
            lastResult = new ObstacleScanResult(sensor.Buffer, count);
            
            // Populate detected buffer for consumers (zero allocation)
            detectedCount = 0;
            for (var i = 0; i < count && i < detectedBuffer.Length; i++)
            {
                var col = sensor.Buffer[i];
                if (col) detectedBuffer[detectedCount++] = new DetectedObstacle(col);
            }
            
            return lastResult;
        }

        // ─────────────────────────────────────────────────────────────
        // Analysis helpers - operate on LastResult
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// All detected obstacles with position and radius.
        /// </summary>
        public IEnumerable<DetectedObstacle> Detected => detectedBuffer.Take(detectedCount);

        /// <summary>
        /// Number of obstacles detected.
        /// </summary>
        public int Count => detectedCount;

        partial void ClearDebugRays();
        partial void AddDebugRay(Vector3 ray);
    }
}
