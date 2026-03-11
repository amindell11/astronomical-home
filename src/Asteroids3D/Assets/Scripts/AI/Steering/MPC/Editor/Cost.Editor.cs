#if UNITY_EDITOR
using Movement;

namespace Movement.MPC
{
    public static partial class Cost
    {
        public static CostBreakdown EvaluateBreakdown(State s, Control u, Control prevU,
            CostInput input, Config cfg, bool isTerminal, int step = 0)
        {
            var stepTime = step * cfg.dt;
            var goalPos = input.goalPos + input.goalVel * stepTime;
            var hasEnemy = !Unity.Mathematics.math.isnan(input.enemyYaw);
            var enemyYaw = hasEnemy ? input.enemyYaw + input.enemyYawRate * stepTime : input.enemyYaw;

            var wVel = cfg.wVel;
            var wYaw = cfg.wYaw;

            var distToGoalSq = Unity.Mathematics.math.lengthsq(goalPos - s.pos);
            if (distToGoalSq < cfg.arrivalDistanceSq)
            {
                var distToGoal = Unity.Mathematics.math.sqrt(distToGoalSq);
                var t = 1f - (distToGoal / cfg.arrivalDistance);
                wVel = Unity.Mathematics.math.lerp(cfg.wVel, cfg.wVel * cfg.arrivalVelScale, t);
                wYaw = Unity.Mathematics.math.lerp(cfg.wYaw, cfg.wYaw * cfg.arrivalYawScale, t);
            }

            var facingTarget = cfg.facingTarget;
            if (input.projectileSpeed > 0f && hasEnemy)
                facingTarget = InterceptYaw(s.pos, goalPos, input.goalVel, input.projectileSpeed);

            var breakdown = new CostBreakdown
            {
                pos = GoalCost(s.pos, goalPos, cfg) * cfg.wPos,
                vel = VelocityCost(s.vel) * wVel,
                heading = HeadingCost(s.pos, s.yaw, goalPos) * wYaw,
                facing = FacingCost(s.yaw, facingTarget, cfg.facingPower) * cfg.wFacing,
                yawRate = YawRateCost(s.yawRate) * cfg.wYawRate,
                obstacle = (cfg.wObstacle > 0f && input.obstacleCount > 0)
                    ? ObstacleCost(s.pos, input.obstacles, input.obstacleCount, cfg.obstacleThreshold) * cfg.wObstacle
                    : 0f,
                los = (hasEnemy && cfg.wLos > 0f && input.obstacleCount > 0)
                    ? LosCost(s.pos, goalPos, input.obstacles, input.obstacleCount) * cfg.wLos
                    : 0f,
                exposure = (hasEnemy && cfg.wExposure > 0f)
                    ? ExposureCost(s.pos, goalPos, enemyYaw, cfg.exposurePower) * cfg.wExposure
                    : 0f,
                tangential = (hasEnemy && cfg.wTangential > 0f)
                    ? TangentialVelocityCost(s.pos, s.vel, goalPos) * cfg.wTangential
                    : 0f,
                effort = EffortCost(u) * cfg.wEffort,
                smoothness = SmoothnessCost(u, prevU, cfg)
            };

            var positionalCost = breakdown.pos + breakdown.vel + breakdown.heading +
                                breakdown.yawRate + breakdown.obstacle;
            var tacticalCost = breakdown.facing + breakdown.los + breakdown.exposure + breakdown.tangential;
            var total = positionalCost + tacticalCost + breakdown.effort + breakdown.smoothness;

            if (isTerminal)
                total += cfg.terminalMultiplier * (positionalCost + tacticalCost);

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
                totalBreakdown.Add(EvaluateBreakdown(current, u, prevU, input, cfg, isTerminal, i));
                current = Model.Step(current, u, cfg, shp);
                prevU = u;
            }

            return totalBreakdown;
        }
    }
}
#endif
