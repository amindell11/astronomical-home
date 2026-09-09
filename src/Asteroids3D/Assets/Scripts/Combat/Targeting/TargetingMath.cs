using Game;
using Movement;
using UnityEngine;
using Utils;

namespace Combat.Targeting
{
    /// <summary>
    /// Stateless aiming geometry: pure functions of a shooter's <see cref="Kinematics"/> and a
    /// target. No binding, no caching — anything that knows a pose can call these (AI, a turret,
    /// a test). The one piece of targeting that genuinely needs state — a frame-coherent
    /// line-of-sight cache — lives in <see cref="LosCache"/>.
    /// </summary>
    public static class TargetingMath
    {
        private static int obstacleMask = -1;

        /// <summary>Layers that block line of sight (asteroids). Resolved lazily, then cached.</summary>
        internal static int ObstacleMask =>
            obstacleMask >= 0 ? obstacleMask : (obstacleMask = LayerIds.Mask(LayerIds.Asteroid));

        public static float AngleTo(in Kinematics self, Vector2 target)
        {
            return (target - self.pos).sqrMagnitude < 0.01f
                ? 0f
                : Vector2.Angle(self.Forward, target - self.pos);
        }

        public static float DistanceTo(in Kinematics self, Vector2 target)
        {
            return (target - self.pos).magnitude;
        }

        /// <summary>Uncached line-of-sight from the shooter to a world-space point.</summary>
        public static bool HasLineOfSight(in Kinematics self, Vector3 targetPos)
        {
            return LineOfSight.IsClear(GamePlane.PlanePointToWorld(self.pos), targetPos, ObstacleMask);
        }

        /// <summary>Uncached line-of-sight between two world-space points.</summary>
        public static bool IsLineClear(Vector3 fromPos, Vector3 targetPos)
        {
            return LineOfSight.IsClear(fromPos, targetPos, ObstacleMask);
        }

        public static Vector2 PredictIntercept(in Kinematics self, Vector2 targetPos, Vector2 targetVel, float projectileSpeed)
        {
            var shooterPos = self.pos;
            var forward = self.Forward;

            var forwardVel = Vector2.Dot(self.vel, forward) * forward;
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

        public static float ClosingSpeed(in Kinematics self, Vector2 targetPos, Vector2 targetVel)
        {
            var toTarget = targetPos - self.pos;
            if (toTarget.sqrMagnitude < 0.0001f) return 0f;
            var relVel = targetVel - self.vel;
            return -Vector2.Dot(toTarget.normalized, relVel);
        }

        public static float AngleFromTarget(in Kinematics self, Vector2 targetPos, Vector2 targetForward)
        {
            var toSelf = self.pos - targetPos;
            return toSelf.sqrMagnitude < 0.01f ? 0f : Vector2.Angle(targetForward, toSelf);
        }
    }
}
