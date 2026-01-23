using UnityEngine;

namespace AI.Scanning
{
    public class DynamicObstacleScanner : ObstacleScanner
    {
        private readonly float baseDistance;
        private readonly float baseDegreesBetweenRays;
        private readonly float baseSpreadAngle;
        private readonly float baseSphereRadius;

        public DynamicObstacleScanner(
            Transform origin, 
            LayerMask obstacleMask, 
            float distance = 10f, 
            float degreesBetweenRays = 10f, 
            float spreadAngle = 90f, 
            float sphereRadius = 0.5f, 
            int bufferSize = 64) 
            : base(origin, obstacleMask, bufferSize)
        {
            baseDistance = distance;
            baseDegreesBetweenRays = degreesBetweenRays;
            baseSpreadAngle = spreadAngle;
            baseSphereRadius = sphereRadius;
        }

        public void Scan(Vector2 vel, float maxSpeed)
        {
            var v = vel.magnitude;
            var t = maxSpeed > 0 ? v / maxSpeed : 0;
            
            var scanDir = v > 0.001f ? vel.normalized : Vector2.up;
            var dist = baseDistance * (1 + 3 * t);
            var spreadAngle = baseSpreadAngle * (1 + 3 * (1 - t));
            var sphereRadius = baseSphereRadius * (1 + 3 * t);
            
            Scan(scanDir, dist, spreadAngle, baseDegreesBetweenRays, sphereRadius);
        }
    }
}
