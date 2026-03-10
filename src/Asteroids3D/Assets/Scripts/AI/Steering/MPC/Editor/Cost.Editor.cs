#if UNITY_EDITOR
using Movement;

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
                pos = GoalCost(s.pos, input.goalPos, cfg) * cfg.wPos,
                vel = VelocityCost(s.vel) * wVel,
                heading = Unity.Mathematics.math.isnan(cfg.facingTarget)
                    ? HeadingCost(s.pos, s.yaw, input.goalPos) * wYaw : 0f,
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

        public static CostBreakdown EvaluateTrajectoryBreakdown(State state, Control[] sequence,
            CostInput input, Config cfg, Dynamics shp, Control lastControl)
        {
            var totalBreakdown = new CostBreakdown();
            var current = state;
            var prevU = lastControl;

            for (var i = 0; i < cfg.horizon; i++)
            {
                var u = sequence[i];
                var isTerminal = i == cfg.horizon - 1;
                totalBreakdown.Add(EvaluateBreakdown(current, u, prevU, input, cfg, isTerminal));
                current = Model.Step(current, u, cfg, shp);
                prevU = u;
            }

            return totalBreakdown;
        }
    }
}
#endif
