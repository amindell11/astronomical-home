using Unity.Mathematics;

namespace Movement.MPC
{
    /// <summary>Composes the fixed cost-term menu. Two axes meet here and nowhere else: the objective terms (<c>Terms/VelocityTrack</c>, <c>Terms/Facing</c>, <c>Terms/Position</c>) are parameterized per decision by the intent sentence and cross the pilot-decision seam, while the solver-owned terms (<c>Terms/Obstacles</c>, <c>Terms/Regularization</c>) are ship character read from <see cref="MpcSettings"/> and never do. Burst rules out a runtime-pluggable term list, so the menu is fixed and the sentence selects within it.</summary>
    public static partial class Cost
    {
        private const float TwoPi = 2f * math.PI;

        /// <summary>Per-step sentence resolution shared by Evaluate and EvaluateBreakdown; unarmed slots take the ×1 legacy path, keeping the default sentence bit-identical.</summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct EvalContext
        {
            public float facingTarget;
            public float facingWeightScale;
            public float2 velocityRef;
            public float velTrackScale;
            public float2 posPoint;
            public float posSetpoint;
            public float posWeightScale;
            public float fieldScale;

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

                var sentence = input.sentence;

                float facingTarget, facingWeightScale;
                if (sentence.aim.armed)
                {
                    // An AIM with no live referent collapses to NaN (FacingCost 0) — the priors carry delegation.
                    facingTarget = ResolveReferent(sentence.aim.referent, input, hasEnemy,
                        enemyPos, enemyVel, enemyYaw, stepTime, out var aimPos, out var aimVel, out _)
                        ? WrapRadians(AnchorYaw(s.pos, aimPos, aimVel, input.projectileSpeed) + sentence.aim.offsetRad)
                        : float.NaN;
                    facingWeightScale = sentence.aim.weight;
                }
                else
                {
                    facingTarget = cfg.facingTarget;
                    facingWeightScale = 1f;
                }

                float2 velocityRef;
                float velTrackScale;
                if (sentence.vel.armed)
                {
                    var hasVelReferent = ResolveReferent(sentence.vel.referent, input, hasEnemy,
                        enemyPos, enemyVel, enemyYaw, stepTime, out var velPos, out var velVel, out _);
                    velocityRef = hasVelReferent
                        ? AnchoredVelocityRef(s.pos, velPos, velVel, sentence.vel.radialSpeed, sentence.vel.tangentialSpeed)
                        : default;
                    velTrackScale = hasVelReferent ? sentence.vel.weight : 0f;
                }
                else if (!math.isnan(input.velocityReference.x))
                {
                    velocityRef = input.velocityReference;
                    velTrackScale = 1f;
                }
                else
                {
                    velocityRef = default;
                    velTrackScale = 0f;
                }

                float2 posPoint = default;
                var posSetpoint = 0f;
                var posWeightScale = 0f;
                if (sentence.pos.armed && ResolveReferent(sentence.pos.referent, input, hasEnemy,
                        enemyPos, enemyVel, enemyYaw, stepTime, out var posRefPos, out var posRefVel, out var posRefYaw))
                {
                    posPoint = posRefPos + sentence.pos.offsetR
                        * Direction(FrameAngle(sentence.pos.frame, posRefYaw, posRefVel) + sentence.pos.offsetThetaRad);
                    posSetpoint = sentence.pos.setpoint;
                    posWeightScale = sentence.pos.weight;
                }

                return new EvalContext
                {
                    facingTarget = facingTarget,
                    facingWeightScale = facingWeightScale,
                    velocityRef = velocityRef,
                    velTrackScale = velTrackScale,
                    posPoint = posPoint,
                    posSetpoint = posSetpoint,
                    posWeightScale = posWeightScale,
                    fieldScale = sentence.field.armed ? sentence.field.weight : 1f,
                };
            }

            /// <summary>Step-resolved referent kinematics: 0 = the caller-resolved enemy, 1–2 = extrapolated snapshots; false = absent, the slot drops to weight 0.</summary>
            private static bool ResolveReferent(int referent, in CostInput input, bool hasEnemy,
                float2 enemyPos, float2 enemyVel, float enemyYaw, float stepTime,
                out float2 pos, out float2 vel, out float yaw)
            {
                switch (referent)
                {
                    case 1: return Extrapolate(input.referent1, stepTime, out pos, out vel, out yaw);
                    case 2: return Extrapolate(input.referent2, stepTime, out pos, out vel, out yaw);
                    default:
                        pos = enemyPos;
                        vel = enemyVel;
                        yaw = enemyYaw;
                        return hasEnemy;
                }
            }

            private static bool Extrapolate(in ReferentSnapshot snapshot, float stepTime,
                out float2 pos, out float2 vel, out float yaw)
            {
                pos = snapshot.pos + snapshot.vel * stepTime;
                vel = snapshot.vel;
                yaw = snapshot.yaw;
                return snapshot.valid;
            }

            /// <summary>The frame's forward angle; the velocity frame falls back to world axes near rest.</summary>
            private static float FrameAngle(ReferentFrame frame, float refYaw, float2 refVel)
            {
                switch (frame)
                {
                    case ReferentFrame.Facing: return refYaw;
                    case ReferentFrame.Velocity:
                        return math.lengthsq(refVel) > 1e-4f ? math.atan2(-refVel.x, refVel.y) : 0f;
                    default: return 0f;
                }
            }

            private static float2 Direction(float yaw) => new(-math.sin(yaw), math.cos(yaw));
        }

        public static float Evaluate(State s, Control u, Control prevU,
            CostInput input, Config cfg, int step = 0)
        {
            var ctx = EvalContext.Create(s, input, cfg, step);
            var profileScale = BankProfileScale(u.strafe, cfg);

            ObstacleCosts(s, input, cfg, profileScale, ctx.fieldScale, out var collisionCost, out var obstacleCost);

            // Control effort (a function of u) and the velocity tracker (regulation, not reaching) stay outside the terminal ramp.
            var stateCost = Aim(s, ctx, cfg)
                + FacingPriorCost(s.yaw, s.vel, cfg)
                + StateRegularizers(s, input, cfg, obstacleCost)
                + Pos(s, ctx, cfg);

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
            ObstacleCosts(s, input, cfg, profileScale, ctx.fieldScale, out var collision, out var obstacle);

            var breakdown = new CostBreakdown
            {
                velocityTrack = VelocityTrackCost(s.vel, ctx.velocityRef, cfg.maxSpeedSq) * cfg.wVelTrack * ctx.velTrackScale,
                facing = Aim(s, ctx, cfg),
                facingPrior = FacingPriorCost(s.yaw, s.vel, cfg),
                pos = Pos(s, ctx, cfg),
                yawRate = YawRateCost(s.yawRate, cfg.maxYawRateSq) * cfg.wYawRate,
                obstacle = obstacle,
                collision = collision,
                momentum = cfg.wMomentum > 0f
                    ? MomentumCost(s.vel, input.initialVel) * cfg.wMomentum
                    : 0f,
                effort = EffortCost(u) * cfg.wEffort,
                smoothness = SmoothnessCost(u, prevU, cfg)
            };

            // Mirrors Cost.Evaluate: the ramp applies to state cost (facing + prior + pos + regularizers); the tracker and control terms stay per-step.
            var stateCost = breakdown.facing + breakdown.facingPrior + breakdown.yawRate + breakdown.obstacle + breakdown.momentum + breakdown.pos;
            var total = stateCost + breakdown.effort + breakdown.smoothness + breakdown.velocityTrack;

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
