#if UNITY_EDITOR
namespace Movement.MPC
{
    public static partial class Cost
    {
        public static CostBreakdown EvaluateBreakdown(State s, Control u, Control prevU,
            CostInput input, Config cfg, bool isTerminal)
        {
            var wVel = cfg.wVel;
            var wYaw = cfg.wYaw;

            var distToGoalSq = Unity.Mathematics.math.lengthsq(input.goalPos - s.pos);
            if (distToGoalSq < cfg.arrivalDistanceSq)
            {
                var distToGoal = Unity.Mathematics.math.sqrt(distToGoalSq);
                var t = 1f - (distToGoal / cfg.arrivalDistance);
                wVel = Unity.Mathematics.math.lerp(cfg.wVel, cfg.wVel * cfg.arrivalVelScale, t);
                wYaw = Unity.Mathematics.math.lerp(cfg.wYaw, cfg.wYaw * cfg.arrivalYawScale, t);
            }

            var breakdown = new CostBreakdown
            {
                pos = PositionCost(s.pos, input.goalPos) * cfg.wPos,
                vel = VelocityCost(s.vel) * wVel,
                heading = HeadingCost(s.pos, s.yaw, input.goalPos) * wYaw,
                facing = FacingCost(s.yaw, cfg.facingTarget) * cfg.wFacing,
                yawRate = YawRateCost(s.yawRate) * cfg.wYawRate,
                obstacle = (cfg.wObstacle > 0f && input.obstacleCount > 0)
                    ? ObstacleCost(s.pos, input.obstacles, input.obstacleCount, cfg.obstacleThreshold) * cfg.wObstacle
                    : 0f,
                effort = EffortCost(u) * cfg.wEffort,
                smoothness = SmoothnessCost(u, prevU, cfg)
            };

            var stateCost = breakdown.pos + breakdown.vel + breakdown.heading +
                            breakdown.facing + breakdown.yawRate + breakdown.obstacle;
            var total = stateCost + breakdown.effort + breakdown.smoothness;

            if (isTerminal)
                total += cfg.terminalMultiplier * stateCost;

            breakdown.total = total;
            return breakdown;
        }
    }
}
#endif
