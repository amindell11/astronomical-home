#if UNITY_EDITOR
using Movement;
using Unity.Mathematics;

namespace Movement.MPC
{
    public static partial class Cost
    {
        public static CostBreakdown EvaluateBreakdown(State s, Control u, Control prevU,
            CostInput input, Config cfg, bool isTerminal, int step = 0)
        {
            var ctx = EvalContext.Create(s, input, cfg, step);

            var breakdown = new CostBreakdown
            {
                pos = (ctx.hasNavTarget
                    ? PositionCost(s.pos, ctx.posCostTarget)
                    : GoalCost(s.pos, ctx.goalPos, cfg)) * cfg.wPos,
                vel = ctx.isFlee ? 0f : VelocityCost(s.vel) * ctx.wVel,
                heading = HeadingCost(s.pos, s.yaw, ctx.headingGoal) * ctx.wYaw,
                facing = FacingCost(s.yaw, ctx.facingTarget, cfg.facingWidth) * cfg.wFacing,
                yawRate = YawRateCost(s.yawRate) * cfg.wYawRate,
                obstacle = (cfg.wObstacle > 0f && input.obstacleCount > 0)
                    ? ObstacleCost(s.pos, input.obstacles, input.obstacleCount,
                        cfg.obstacleThreshold + math.length(s.vel) * cfg.obstacleSpeedMargin) * cfg.wObstacle
                    : 0f,
                los = (ctx.hasEnemy && cfg.wLos > 0f && input.obstacleCount > 0)
                    ? LosCost(s.pos, ctx.goalPos, input.obstacles, input.obstacleCount) * cfg.wLos
                    : 0f,
                exposure = (ctx.hasEnemy && cfg.wExposure > 0f)
                    ? ExposureCost(s.pos, ctx.goalPos, ctx.enemyYaw, cfg.exposureWidth) * cfg.wExposure
                    : 0f,
                tangential = (ctx.hasEnemy && cfg.wTangential > 0f)
                    ? TangentialVelocityCost(s.pos, s.vel, ctx.goalPos) * cfg.wTangential
                    : 0f,
                momentum = cfg.wMomentum > 0f
                    ? MomentumCost(s.vel, input.initialVel) * cfg.wMomentum
                    : 0f,
                effort = EffortCost(u) * cfg.wEffort,
                boostEffort = u.boost * u.boost * cfg.wBoostEffort,
                smoothness = SmoothnessCost(u, prevU, cfg)
            };

            var positionalCost = breakdown.pos + breakdown.vel + breakdown.heading +
                                breakdown.yawRate + breakdown.obstacle + breakdown.momentum;
            var tacticalCost = breakdown.facing + breakdown.los + breakdown.exposure + breakdown.tangential;
            var total = positionalCost + tacticalCost + breakdown.effort + breakdown.boostEffort + breakdown.smoothness;

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
