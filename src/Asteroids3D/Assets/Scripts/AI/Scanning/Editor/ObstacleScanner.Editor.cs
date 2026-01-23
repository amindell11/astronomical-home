#if UNITY_EDITOR
using System.Collections.Generic;
using Game;
using UnityEngine;

namespace AI.Scanning
{
    public partial class ObstacleScanner
    {
        private Vector3 debugDirection;
        private float debugDistance;
        private float debugSpreadAngle;
        private float debugDegreesBetweenRays;
        private float debugSphereRadius;

        public float LastSphereRadius => debugSphereRadius;

        partial void RecordDebugParams(Vector3 direction, float dist, float spreadAngle, float degreesBetweenRays, float sphereRadius)
        {
            debugDirection = direction;
            debugDistance = dist;
            debugSpreadAngle = spreadAngle;
            debugDegreesBetweenRays = degreesBetweenRays;
            debugSphereRadius = sphereRadius;
        }

        public IEnumerable<Vector3> DebugRays
        {
            get
            {
                if (debugDegreesBetweenRays <= 0) yield break;
                
                yield return debugDirection * debugDistance;
                
                var raysPerSide = Mathf.FloorToInt(debugSpreadAngle / debugDegreesBetweenRays);
                var planeNormal = GamePlane.Normal;
                
                for (var i = 1; i <= raysPerSide; i++)
                {
                    var angle = i * debugDegreesBetweenRays;
                    yield return Quaternion.AngleAxis(-angle, planeNormal) * debugDirection * debugDistance;
                    yield return Quaternion.AngleAxis(angle, planeNormal) * debugDirection * debugDistance;
                }
            }
        }
    }
}
#endif

