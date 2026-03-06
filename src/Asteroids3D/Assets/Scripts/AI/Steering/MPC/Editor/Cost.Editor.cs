#if UNITY_EDITOR
using AI.Scanning;
using UnityEngine;
using UnityEngine.Profiling;

namespace Movement.MPC
{
    public static partial class Cost
    {
        static partial void BeginEvaluateProfiling() => Profiler.BeginSample("MPC.Cost.Evaluate");

        static partial void EndEvaluateProfiling() => Profiler.EndSample();

        public static CostBreakdown EvaluateBreakdown(State s, Control u, Control prevU, Vector2 goalPos, 
            ObstacleScan scan, Config cfg, bool isTerminal)
        {
            var breakdown = new CostBreakdown();
            breakdown.pos = PositionCost(s.pos, goalPos) * cfg.wPos;
            breakdown.vel = VelocityCost(s.vel) * cfg.wVel;
            breakdown.heading = HeadingCost(s.pos, s.yaw, goalPos) * cfg.wYaw;
            breakdown.facing = FacingCost(s.yaw, cfg.facingTarget) * cfg.wFacing;
            breakdown.yawRate = YawRateCost(s.yawRate) * cfg.wYawRate;
            breakdown.obstacle = ObstacleCost(s.pos, scan, cfg.obstacleThreshold) * cfg.wObstacle;

            var stateCost = breakdown.pos + breakdown.vel + breakdown.heading + breakdown.facing + breakdown.yawRate + breakdown.obstacle;

            breakdown.effort = EffortCost(u) * cfg.wEffort;
            breakdown.smoothness = SmoothnessCost(u, prevU, cfg);
            var controlCost = breakdown.effort + breakdown.smoothness;

            var total = stateCost + controlCost;

            if (isTerminal)
            {
                var termExtra = cfg.terminalMultiplier * stateCost;
                total += termExtra;
            }

            breakdown.total = total;
            return breakdown;
        }
    }
}
#endif
