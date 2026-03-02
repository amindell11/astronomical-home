using AI.Context;
using UnityEngine;
using Utils;

namespace Combat
{
    public class TargetingUtils
    {
        private const int DefaultLosCacheFrames = 5;
        private const float DefaultAngleToleranceBeforeRay = 15f;

        private readonly LayerMask obstacleMask;
        private readonly ShipInfo shipInfo;
        private readonly CombatTuning tuning;

        private bool cachedLos;
        private Vector3 lastFirePos;
        private Vector3 lastTargetPos;
        private int losFrame = -1;

        public TargetingUtils(ShipInfo shipInfo, CombatTuning tuning = null)
        {
            this.shipInfo = shipInfo;
            this.tuning = tuning;
            obstacleMask = LayerIds.Mask(LayerIds.Asteroid);
        }

        public float AngleTo(Vector2 target)
        {
            return (target - shipInfo.Pos).sqrMagnitude < 0.01f
                ? 0f
                : Vector2.Angle(shipInfo.Forward, target - shipInfo.Pos);
        }

        public float DistanceTo(Vector2 target)
        {
            return (target - shipInfo.Pos).magnitude;
        }

        public Vector2 VectorTo(Vector2 target)
        {
            return target - shipInfo.Pos;
        }

        public bool HasLineOfSight(Vector3 targetPos)
        {
            return LineOfSight.IsClear(shipInfo.Pos3D, targetPos, obstacleMask);
        }

        public bool HasLineOfSight(Vector3 firePos, Vector3 targetPos, float angleToTarget)
        {
            if (angleToTarget > AngleToleranceBeforeRay)
                return false;

            var frame = Time.frameCount;
            var needsUpdate = losFrame < 0 || frame - losFrame >= LosCacheFrames
                                           || Vector3.Distance(firePos, lastFirePos) > 1f
                                           || Vector3.Distance(targetPos, lastTargetPos) > 1f;

            if (!needsUpdate)
                return cachedLos;

            cachedLos = LineOfSight.IsClear(firePos, targetPos, obstacleMask);
            losFrame = frame;
            lastFirePos = firePos;
            lastTargetPos = targetPos;
            return cachedLos;
        }

        public Vector2 PredictIntercept(Vector2 targetPos, Vector2 targetVel, float projectileSpeed)
        {
            var shooterPos = shipInfo.Pos;
            var shooterVel = shipInfo.Vel;
            var forward = shipInfo.Forward;

            var forwardVel = Vector2.Dot(shooterVel, forward) * forward;
            var relPos = targetPos - shooterPos;
            var relVel = targetVel - forwardVel;

            var a = Vector2.Dot(relVel, relVel) - projectileSpeed * projectileSpeed;
            var b = 2f * Vector2.Dot(relVel, relPos);
            var c = Vector2.Dot(relPos, relPos);

            float t;
            const float eps = 0.0001f;

            if (Mathf.Abs(a) < eps)
            {
                t = Mathf.Abs(b) < eps ? 0f : -c / b;
            }
            else
            {
                var disc = b * b - 4f * a * c;
                if (disc < 0f)
                {
                    t = 0f;
                }
                else
                {
                    var sqrtDisc = Mathf.Sqrt(disc);
                    var t1 = (-b + sqrtDisc) / (2f * a);
                    var t2 = (-b - sqrtDisc) / (2f * a);
                    t = t1 > 0f && t2 > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
                    if (t < 0f) t = 0f;
                }
            }

            return targetPos + targetVel * t;
        }

        public float ClosingSpeed(Vector2 targetPos, Vector2 targetVel)
        {
            var toTarget = targetPos - shipInfo.Pos;
            if (toTarget.sqrMagnitude < 0.0001f) return 0f;
            var relVel = targetVel - shipInfo.Vel;
            return -Vector2.Dot(toTarget.normalized, relVel);
        }

        public float AngleFromTarget(Vector2 targetPos, Vector2 targetForward)
        {
            var toSelf = shipInfo.Pos - targetPos;
            return toSelf.sqrMagnitude < 0.01f ? 0f : Vector2.Angle(targetForward, toSelf);
        }

        public Vector2 RelativeVelocity(Vector2 targetVel)
        {
            return targetVel - shipInfo.Vel;
        }

        private int LosCacheFrames => tuning ? Mathf.Max(1, tuning.LineOfSightCacheFrames) : DefaultLosCacheFrames;
        private float AngleToleranceBeforeRay => tuning ? tuning.AngleToleranceBeforeRaycast : DefaultAngleToleranceBeforeRay;
    }
}
