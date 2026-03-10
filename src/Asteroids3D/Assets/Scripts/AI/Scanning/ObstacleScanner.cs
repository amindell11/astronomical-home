using System.Collections.Generic;
using System.Linq;
using Game;
using Sensors;
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

    public partial class ObstacleScanner
    {
        private static readonly Collider[] ScratchBuffer = new Collider[128];
        
        protected readonly Transform origin;
        protected readonly LayerMask obstacleMask;
        protected readonly GameObject selfRoot;
        private Transform excludeRoot;

        public DetectedObstacle[] DetectedBuffer { get; }
        public int DetectedCount { get; private set; }
        public IEnumerable<DetectedObstacle> Detected => DetectedBuffer.Take(DetectedCount);

        public void SetExcludeRoot(Transform root) => excludeRoot = root;
        public void ClearExcludeRoot() => excludeRoot = null;

        protected ObstacleScanner(Transform origin, LayerMask obstacleMask, int bufferSize = 64)
        {
            this.origin = origin;
            this.obstacleMask = obstacleMask;
            selfRoot = origin.gameObject;
            DetectedBuffer = new DetectedObstacle[bufferSize];
            DetectedCount = 0;
        }

        protected void Scan(Vector2 scanDir, float dist, float spreadAngle, float degreesBetweenRays, float sphereRadius)
        {
            System.Array.Clear(ScratchBuffer, 0, ScratchBuffer.Length);
            
            var direction = GamePlane.PlaneDirToWorld(scanDir).normalized;
            RecordDebugParams(direction, dist, spreadAngle, degreesBetweenRays, sphereRadius);
            
            var count = RayFanSensor.Detect(origin.position, 
                direction, 
                dist, 
                spreadAngle, 
                degreesBetweenRays, 
                sphereRadius, 
                obstacleMask, 
                ScratchBuffer);
            
            DetectedCount = 0;
            for (var i = 0; i < count && DetectedCount < DetectedBuffer.Length; i++)
            {
                var col = ScratchBuffer[i];
                if (col && col.gameObject != selfRoot && col.transform.root != origin.root
                    && (!excludeRoot || col.transform.root != excludeRoot))
                    DetectedBuffer[DetectedCount++] = new DetectedObstacle(col);
            }
        }
        
        partial void RecordDebugParams(Vector3 direction, float dist, float spreadAngle, float degreesBetweenRays, float sphereRadius);
    }
}

