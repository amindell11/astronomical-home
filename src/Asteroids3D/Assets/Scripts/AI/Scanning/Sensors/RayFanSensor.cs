using System;
using Game;
using UnityEngine;

namespace AI.Scanning.Sensors
{
    public class RayFanSensor : IDirectionalSensor
    {
        private readonly Collider[] buffer;
        private readonly RaycastHit[] rayHits;
        private readonly Transform origin;
        private readonly LayerMask layerMask;
        private readonly float distance;
        private readonly int raysPerSide;
        private readonly float spreadAngle;
        private readonly float sphereRadius;

        private readonly Vector3[] directions;
        private int directionCount;

        public Collider[] Buffer => buffer;
        public Vector3[] Directions => directions;
        public int DirectionCount => directionCount;

        public RayFanSensor(Transform origin, float distance, LayerMask layerMask, int raysPerSide = 5, float spreadAngle = 90f, float sphereRadius = 0f, int bufferSize = 64)
        {
            this.origin = origin;
            this.distance = distance;
            this.layerMask = layerMask;
            this.raysPerSide = raysPerSide;
            this.spreadAngle = spreadAngle;
            this.sphereRadius = sphereRadius;
            buffer = new Collider[bufferSize];
            rayHits = new RaycastHit[bufferSize];
            directions = new Vector3[1 + raysPerSide * 2];
        }

        public int Detect() => Detect(origin.forward);

        public int Detect(Vector3 direction)
        {
            var pos = origin.position;
            directionCount = 0;
            
            // Central ray
            directions[directionCount++] = direction;
            var hitCount = CastRay(pos, direction, 0);

            if (raysPerSide <= 0) return hitCount;

            var angleStep = spreadAngle / raysPerSide;
            var planeNormal = GamePlane.Normal;

            for (var i = 1; i <= raysPerSide; i++)
            {
                var angle = i * angleStep;
                var leftDir = Quaternion.AngleAxis(-angle, planeNormal) * direction;
                var rightDir = Quaternion.AngleAxis(angle, planeNormal) * direction;

                directions[directionCount++] = leftDir;
                directions[directionCount++] = rightDir;

                hitCount = CastRay(pos, leftDir, hitCount);
                hitCount = CastRay(pos, rightDir, hitCount);
            }

            return hitCount;
        }

        private int CastRay(Vector3 pos, Vector3 dir, int startIndex)
        {
            var count = sphereRadius > 0f
                ? Physics.SphereCastNonAlloc(pos, sphereRadius, dir, rayHits, distance, layerMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(pos, dir, rayHits, distance, layerMask, QueryTriggerInteraction.Ignore);

            var n = startIndex;
            for (var i = 0; i < count && n < buffer.Length; i++)
            {
                var col = rayHits[i].collider;
                if (col && Array.IndexOf(buffer, col, 0, n) < 0)
                    buffer[n++] = col;
            }
            return n;
        }
    }
}
