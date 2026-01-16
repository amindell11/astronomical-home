using System;
using Game;
using Ships.Movement;
using UnityEngine;

namespace AI.Computers
{
    public partial class ObstacleScanner
    {
        private const int MaxColliders = 256;
        private readonly Collider[] hits = new Collider[MaxColliders];
        private readonly RaycastHit[] rayHits = new RaycastHit[MaxColliders];
        private readonly Transform origin;

        public ObstacleScanner(Transform origin)
        {
            this.origin = origin;
        }

        public ScanResult Scan(Config config, Kinematics kin)
        {
            var result = new ScanResult(hits);
            ClearDebugRays();

            if (!config.enabled) return result;

            var maxDist = config.maxSpeed * config.lookAheadTime + config.safeMargin;
            var centerDir2D = kin.Vel.sqrMagnitude > 0.1f ? kin.Vel.normalized : kin.Forward;
            var centerDirWorld = GamePlane.PlaneDirToWorld(centerDir2D).normalized;
            var pos = origin.position;

            result.hitCount = CastAndCollect(pos, centerDirWorld, maxDist, config, 0);
            AddDebugRay(centerDirWorld * maxDist);

            if (config.raysPerDirection <= 0) return result;

            var angleStep = config.maxRayDegrees / config.raysPerDirection;
            for (var i = 1; i <= config.raysPerDirection; i++)
            {
                var angle = i * angleStep;

                var leftDir = Quaternion.Euler(0, -angle, 0) * centerDirWorld;
                result.hitCount = CastAndCollect(pos, leftDir, maxDist, config, result.hitCount);
                AddDebugRay(leftDir * maxDist);

                var rightDir = Quaternion.Euler(0, angle, 0) * centerDirWorld;
                result.hitCount = CastAndCollect(pos, rightDir, maxDist, config, result.hitCount);
                AddDebugRay(rightDir * maxDist);
            }

            return result;
        }

        private int CastAndCollect(Vector3 pos, Vector3 dir, float maxDist, Config config, int start)
        {
            var n = start;
            var cnt = config.sphereCastRadius > 0f
                ? Physics.SphereCastNonAlloc(pos, config.sphereCastRadius, dir, rayHits, maxDist, config.asteroidMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(pos, dir, rayHits, maxDist, config.asteroidMask, QueryTriggerInteraction.Ignore);

            for (var i = 0; i < cnt && n < MaxColliders; i++)
            {
                var col = rayHits[i].collider;
                if (col && Array.IndexOf(hits, col, 0, n) < 0) hits[n++] = col;
            }
            return n;
        }

        // Partial methods - removed entirely in production when not implemented
        partial void ClearDebugRays();
        partial void AddDebugRay(Vector3 ray);

        public struct Config
        {
            public bool enabled;
            public LayerMask asteroidMask;
            public float lookAheadTime;
            public float safeMargin;
            public float maxSpeed;
            public int raysPerDirection;
            public float maxRayDegrees;
            public float sphereCastRadius;
        }

        public struct ScanResult
        {
            public int hitCount;
            public ArraySegment<Collider> Obstacles => 
                colliders != null 
                    ? new ArraySegment<Collider>(colliders, 0, hitCount) 
                    : new ArraySegment<Collider>();
            
            private readonly Collider[] colliders;

            public ScanResult(Collider[] buffer)
            {
                colliders = buffer;
                hitCount = 0;
            }
        }
    }
}
