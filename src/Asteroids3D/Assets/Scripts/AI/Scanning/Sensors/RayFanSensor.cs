using System;
using Game;
using UnityEngine;

namespace AI.Scanning.Sensors
{
    public static class RayFanSensor
    {
        private static readonly RaycastHit[] RayHits = new RaycastHit[128];

        public static int Detect(
            Transform origin, 
            Vector3 direction, 
            float dist, 
            float spreadAngle, 
            float degreesBetweenRays, 
            float sphereRadius, 
            LayerMask layerMask,
            Collider[] colliderBuffer)
        {
            var pos = origin.position;
            var hitCount = CastRay(pos, direction, 0, dist, sphereRadius, layerMask, colliderBuffer);

            if (degreesBetweenRays <= 0) return hitCount;

            var raysPerSide = Mathf.FloorToInt(spreadAngle / degreesBetweenRays);
            var planeNormal = GamePlane.Normal;

            for (var i = 1; i <= raysPerSide; i++)
            {
                var angle = i * degreesBetweenRays;
                var leftDir = Quaternion.AngleAxis(-angle, planeNormal) * direction;
                var rightDir = Quaternion.AngleAxis(angle, planeNormal) * direction;

                hitCount = CastRay(pos, leftDir, hitCount, dist, sphereRadius, layerMask, colliderBuffer);
                hitCount = CastRay(pos, rightDir, hitCount, dist, sphereRadius, layerMask, colliderBuffer);
            }

            return hitCount;
        }

        private static int CastRay(Vector3 pos, Vector3 dir, int startIndex, float dist, float radius, LayerMask layerMask, Collider[] buffer)
        {
            var count = radius > 0f
                ? Physics.SphereCastNonAlloc(pos, radius, dir, RayHits, dist, layerMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(pos, dir, RayHits, dist, layerMask, QueryTriggerInteraction.Ignore);

            var n = startIndex;
            for (var i = 0; i < count && n < buffer.Length; i++)
            {
                var col = RayHits[i].collider;
                if (col && Array.IndexOf(buffer, col, 0, n) < 0)
                    buffer[n++] = col;
            }
            return n;
        }
    }
}
