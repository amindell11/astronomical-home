using Unity.Mathematics;

namespace Movement.MPC
{
    /// <summary>Composes the fixed cost-term menu. Two axes meet here and nowhere else: the objective terms (<c>Terms/VelocityTrack</c>, <c>Terms/Facing</c>) are parameterized per decision and cross the pilot-decision seam, while the solver-owned terms (<c>Terms/Obstacles</c>, <c>Terms/Regularization</c>) are ship character read from <see cref="MpcSettings"/> and never do. Burst rules out a runtime-pluggable term list, so the menu is fixed and the objective selects within it.</summary>
    public static partial class Cost
    {
        private const float TwoPi = 2f * math.PI;

        /// <summary>Preprocessed per-step context shared by Evaluate and EvaluateBreakdown: the predicted enemy this step, the resolved facing target, and the resolved velocity reference with their authority scales (×1 on the legacy world-frame path).</summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct EvalContext
        {
            public float2 enemyPos;
            public float2 enemyVel;
            public float enemyYaw;
            public bool hasEnemy;
            public float facingTarget;
            public float facingWeightScale;
            public float2 velocityRef;
            public float velTrackScale;

            internal static EvalContext Create(State s, CostInput input, Config cfg, int step)
            {
                var stepTime = step * cfg.dt;
                var hasEnemy = !math.isnan(input.enemyYaw);

                var haveTrack = input.enemyStates.IsCreated && step < input.enemyStateCount;
                float2 enemyPos, enemyVel;
                float enemyYaw;
                if (haveTrack)
                {
                    var es = input.enemyStates[step];
                    enemyPos = es.pos;
                    enemyVel = es.vel;
                    enemyYaw = hasEnemy ? es.yaw : input.enemyYaw;
                }
                else
                {
                    enemyPos = input.enemyPos + input.enemyVel * stepTime;
                    enemyVel = input.enemyVel;
                    enemyYaw = hasEnemy ? input.enemyYaw + input.enemyYawRate * stepTime : input.enemyYaw;
                }

                var anchored = input.anchored;

                float facingTarget, facingWeightScale;
                if (anchored.hasFacing)
                {
                    // Anchored with no enemy collapses to NaN (FacingCost 0) — the priors carry delegation.
                    facingTarget = hasEnemy
                        ? WrapRadians(AnchorYaw(s.pos, enemyPos, enemyVel, input.projectileSpeed) + anchored.facingOffsetRad)
                        : float.NaN;
                    facingWeightScale = anchored.facingWeight;
                }
                else
                {
                    facingTarget = cfg.facingTarget;
                    facingWeightScale = 1f;
                }

                float2 velocityRef;
                float velTrackScale;
                if (anchored.hasVelocity)
                {
                    velocityRef = hasEnemy ? AnchoredVelocityRef(s.pos, enemyPos, enemyVel, anchored) : default;
                    velTrackScale = hasEnemy ? anchored.velocityWeight : 0f;
                }
                else
                {
                    velocityRef = input.velocityReference;
                    velTrackScale = 1f;
                }

                return new EvalContext
                {
                    enemyPos = enemyPos,
                    enemyVel = enemyVel,
                    enemyYaw = enemyYaw,
                    hasEnemy = hasEnemy,
                    facingTarget = facingTarget,
                    facingWeightScale = facingWeightScale,
                    velocityRef = velocityRef,
                    velTrackScale = velTrackScale,
                };
            }
        }

        public static float Evaluate(State s, Control u, Control prevU,
            CostInput input, Config cfg, int step = 0)
        {
            var ctx = EvalContext.Create(s, input, cfg, step);
            var profileScale = BankProfileScale(u.strafe, cfg);

            ObstacleCosts(s, input, cfg, profileScale, out var collisionCost, out var obstacleCost);

            // Control effort (a function of u) and the velocity tracker (regulation, not reaching) stay outside the terminal ramp.
            var stateCost = Aim(s, ctx, cfg)
                + FacingPriorCost(s.yaw, s.vel, cfg)
                + StateRegularizers(s, input, cfg, obstacleCost);

            var perStepCost = ControlCost(u, prevU, cfg)
                + VelocityTrackCost(s.vel, ctx.velocityRef, cfg.maxSpeedSq) * cfg.wVelTrack * ctx.velTrackScale;

            var total = stateCost + perStepCost;
            if (cfg.terminalMultiplier > 0f && cfg.horizon > 1)
            {
                var t = step / (float)(cfg.horizon - 1);
                total += math.pow(t, cfg.terminalCurve) * cfg.terminalMultiplier * stateCost;
            }

            return total + collisionCost;
        }

        internal static float WrapRadians(float angle)
        {
            if (angle > math.PI) return angle - TwoPi;
            if (angle < -math.PI) return angle + TwoPi;
            return angle;
        }

        // Unguarded: the trajectory painter compiles into the player.
        public static CostBreakdown EvaluateBreakdown(State s, Control u, Control prevU,
            CostInput input, Config cfg, int step = 0)
        {
            var ctx = EvalContext.Create(s, input, cfg, step);
            var profileScale = BankProfileScale(u.strafe, cfg);

            // Shares Evaluate's obstacle resolution so the two can't drift.
            ObstacleCosts(s, input, cfg, profileScale, out var collision, out var obstacle);

            var breakdown = new CostBreakdown
            {
                velocityTrack = VelocityTrackCost(s.vel, ctx.velocityRef, cfg.maxSpeedSq) * cfg.wVelTrack * ctx.velTrackScale,
                facing = Aim(s, ctx, cfg),
                facingPrior = FacingPriorCost(s.yaw, s.vel, cfg),
                yawRate = YawRateCost(s.yawRate, cfg.maxYawRateSq) * cfg.wYawRate,
                obstacle = obstacle,
                collision = collision,
                momentum = cfg.wMomentum > 0f
                    ? MomentumCost(s.vel, input.initialVel) * cfg.wMomentum
                    : 0f,
                effort = EffortCost(u) * cfg.wEffort,
                boostEffort = u.boost * u.boost * cfg.wBoostEffort,
                smoothness = SmoothnessCost(u, prevU, cfg)
            };

            // Mirrors Cost.Evaluate: the ramp applies to state cost (facing + prior + regularizers); the tracker and control terms stay per-step.
            var stateCost = breakdown.facing + breakdown.facingPrior + breakdown.yawRate + breakdown.obstacle + breakdown.momentum;
            var total = stateCost + breakdown.effort + breakdown.boostEffort +
                        breakdown.smoothness + breakdown.velocityTrack;

            if (cfg.terminalMultiplier > 0f && cfg.horizon > 1)
            {
                var t = step / (float)(cfg.horizon - 1);
                total += math.pow(t, cfg.terminalCurve) * cfg.terminalMultiplier * stateCost;
            }

            // Collision is a fixed penalty outside the terminal ramp.
            breakdown.total = total + breakdown.collision;
            return breakdown;
        }

#if UNITY_EDITOR
        public static CostBreakdown EvaluateTrajectoryBreakdown(State state, Control[] sequence,
            CostInput input, Config cfg, Dynamics shp, Control lastControl)
        {
            var totalBreakdown = new CostBreakdown();
            var current = state;
            var prevU = lastControl;

            for (var i = 0; i < cfg.horizon; i++)
            {
                var u = sequence[i];
                totalBreakdown.Add(EvaluateBreakdown(current, u, prevU, input, cfg, i));
                current = Model.Step(current, u, cfg, shp);
                prevU = u;
            }

            return totalBreakdown;
        }
#endif
    }
}
