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
            var profileScale = BankProfileScale(u.strafe, cfg);

            // Shares Evaluate's obstacle resolution so the two can't drift: collision and
            // turn-away are mutually exclusive per step.
            ObstacleCosts(s, input, cfg, profileScale, out var collision, out var obstacle);
            var tacticalOn = cfg.tacticalEnabled;
            var velocityMode = cfg.goalMode == GoalMode.VelocityReference;

            var breakdown = new CostBreakdown
            {
                pos = velocityMode ? 0f : PositionalGoalCost(s.pos, ctx, cfg) * cfg.wPos,
                vel = velocityMode ? 0f : VelocityCost(s.vel, cfg.maxSpeedSq) * ctx.wVel,
                closing = velocityMode || ctx.wClosing == 0f ? 0f
                    : ClosingCost(s.pos, s.vel, ctx.goalTarget, cfg.maxSpeedSq, cfg.closingFadeDistance) * ctx.wClosing,
                heading = velocityMode ? 0f : HeadingCost(s.pos, s.yaw, ctx.headingGoal, cfg.wYawDistanceScale) * ctx.wYaw,
                velocityTrack = velocityMode
                    ? VelocityTrackCost(s.vel, input.velocityReference, cfg.maxSpeedSq) * cfg.wVelTrack : 0f,
                facing = FacingCost(s.yaw, ctx.facingTarget, cfg.facingWidth) * cfg.wFacing,
                yawRate = YawRateCost(s.yawRate, cfg.maxYawRateSq) * cfg.wYawRate,
                obstacle = obstacle,
                collision = collision,
                los = (tacticalOn && ctx.hasEnemy && cfg.wLos > 0f && input.obstacleCount > 0)
                    ? LosCost(s.pos, ctx.enemyPos, input.obstacles, input.obstacleCount) * cfg.wLos
                    : 0f,
                exposure = (tacticalOn && ctx.hasEnemy && cfg.wExposure > 0f)
                    ? ExposureCost(s.pos, ctx.enemyPos, ctx.enemyYaw, cfg.exposureWidth) * cfg.wExposure
                    : 0f,
                tangential = (tacticalOn && ctx.hasEnemy && cfg.wTangential > 0f)
                    ? TangentialVelocityCost(s.pos, s.vel, ctx.enemyPos) * cfg.wTangential
                    : 0f,
                missDistance = (tacticalOn && ctx.hasEnemy && cfg.wMissDistance > 0f && input.projectileSpeed > 0f)
                    ? MissDistanceCost(s.pos, s.vel, ctx.enemyPos, input.projectileSpeed,
                        profileScale) * cfg.wMissDistance
                    : 0f,
                momentum = cfg.wMomentum > 0f
                    ? MomentumCost(s.vel, input.initialVel) * cfg.wMomentum
                    : 0f,
                effort = EffortCost(u) * cfg.wEffort,
                boostEffort = u.boost * u.boost * cfg.wBoostEffort,
                smoothness = SmoothnessCost(u, prevU, cfg)
            };

            var positionalCost = breakdown.pos + breakdown.vel + breakdown.closing + breakdown.heading +
                                breakdown.yawRate + breakdown.obstacle + breakdown.momentum;
            var tacticalCost = breakdown.facing + breakdown.los + breakdown.exposure + breakdown.tangential + breakdown.missDistance;
            // velocityTrack is the un-ramped objective (mirrors Cost.Evaluate): in the base total
            // but excluded from the terminal ramp.
            var total = positionalCost + tacticalCost + breakdown.effort + breakdown.boostEffort +
                        breakdown.smoothness + breakdown.velocityTrack;

            if (cfg.terminalMultiplier > 0f && cfg.horizon > 1)
            {
                var t = step / (float)(cfg.horizon - 1);
                var ramp = math.pow(t, cfg.terminalCurve) * cfg.terminalMultiplier;
                total += ramp * (positionalCost + tacticalCost);
            }

            // Mirrors Cost.Evaluate: collision is a fixed penalty outside the terminal ramp.
            breakdown.total = total + breakdown.collision;
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
