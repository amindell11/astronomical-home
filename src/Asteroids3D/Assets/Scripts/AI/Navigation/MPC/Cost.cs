using Unity.Mathematics;

namespace Movement.MPC
{
    public static class Cost
    {
        private const float TwoPi = 2f * math.PI;

        /// <summary>Preprocessed per-step context shared by Evaluate and EvaluateBreakdown: the predicted enemy this step and the resolved facing target.</summary>
        internal struct EvalContext
        {
            public float2 enemyPos;
            public float2 enemyVel;
            public float enemyYaw;
            public bool hasEnemy;
            public float facingTarget;

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

                var facingTarget = cfg.facingTarget;
                if (input.projectileSpeed > 0f && hasEnemy)
                    facingTarget = InterceptYaw(s.pos, enemyPos, enemyVel, input.projectileSpeed);

                return new EvalContext
                {
                    enemyPos = enemyPos,
                    enemyVel = enemyVel,
                    enemyYaw = enemyYaw,
                    hasEnemy = hasEnemy,
                    facingTarget = facingTarget,
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
                + StateRegularizers(s, input, cfg, obstacleCost);

            var perStepCost = ControlCost(u, prevU, cfg)
                + VelocityTrackCost(s.vel, input.velocityReference, cfg.maxSpeedSq) * cfg.wVelTrack;

            var total = stateCost + perStepCost;
            if (cfg.terminalMultiplier > 0f && cfg.horizon > 1)
            {
                var t = step / (float)(cfg.horizon - 1);
                total += math.pow(t, cfg.terminalCurve) * cfg.terminalMultiplier * stateCost;
            }

            return total + collisionCost;
        }

        /// <summary>Intercept-facing geometry. Ramped; 0 when no facing target is set.</summary>
        internal static float Aim(State s, in EvalContext ctx, in Config cfg)
            => FacingCost(s.yaw, ctx.facingTarget, cfg.facingWidth) * cfg.wFacing;

        /// <summary>State regularizers (obstacle turn-away, yaw-rate damping, momentum) — state functions that ride the terminal ramp; always on. Takes the pre-resolved <paramref name="obstacleCost"/> from <see cref="ObstacleCosts"/>.</summary>
        internal static float StateRegularizers(State s, in CostInput input, in Config cfg, float obstacleCost)
        {
            var yawRate = YawRateCost(s.yawRate, cfg.maxYawRateSq) * cfg.wYawRate;
            var momentum = cfg.wMomentum > 0f ? MomentumCost(s.vel, input.initialVel) * cfg.wMomentum : 0f;
            return obstacleCost + yawRate + momentum;
        }

        /// <summary>Control cost (effort, boost, smoothness) — a function of the input u, not the state, so it is per-step and never ramped.</summary>
        internal static float ControlCost(Control u, Control prevU, in Config cfg)
            => EffortCost(u) * cfg.wEffort + u.boost * u.boost * cfg.wBoostEffort + SmoothnessCost(u, prevU, cfg);

        /// <summary>Bank profile: cos(strafe * maxBank) is the fraction of the ship's cross-section visible in-plane — banking rolls the collider, narrowing the hull. Drives obstacle clearance.</summary>
        internal static float BankProfileScale(float strafe, in Config cfg)
            => cfg.maxBankAngleRad > 0f ? math.cos(math.abs(strafe) * cfg.maxBankAngleRad) : 1f;

        /// <summary>The hard collision penalty (fixed, un-ramped) and the gated turn-away cost (ramped) are mutually exclusive per step: an overlapping hull pays only the penalty, a clear hull only turn-away.</summary>
        internal static void ObstacleCosts(State s, in CostInput input, in Config cfg,
            float profileScale, out float collision, out float obstacle)
        {
            collision = 0f;
            obstacle = 0f;
            if (input.obstacleCount <= 0 || (cfg.collisionPenalty <= 0f && cfg.wObstacle <= 0f)) return;

            var hullRadius = cfg.shipRadius * profileScale + cfg.collisionSafetyMargin;
            if (Collides(s.pos, input.obstacles, input.obstacleCount, hullRadius))
                collision = cfg.collisionPenalty;
            else if (cfg.wObstacle > 0f)
                obstacle = TurnAwayCost(s.pos, s.vel, input.obstacles, input.obstacleCount,
                    hullRadius, cfg.maxLatAccel) * cfg.wObstacle;
        }

        /// <summary>Yaw to a first-order intercept point, using t = dist / projectileSpeed as the time-of-flight estimate.</summary>
        internal static float InterceptYaw(float2 shipPos, float2 targetPos, float2 targetVel, float projectileSpeed)
        {
            var toTarget = targetPos - shipPos;
            var dist = math.length(toTarget);
            if (dist < 1e-4f) return math.atan2(-toTarget.x, toTarget.y);
            var tof = dist / projectileSpeed;
            var intercept = targetPos + targetVel * tof;
            var toIntercept = intercept - shipPos;
            return math.atan2(-toIntercept.x, toIntercept.y);
        }

        /// <summary>Squared velocity-tracking error normalized by maxSpeed²; 0 at the reference. World-plane throughout — State.vel and the reference share the frame, so no conversion.</summary>
        internal static float VelocityTrackCost(float2 vel, float2 velocityReference, float maxSpeedSq) =>
            maxSpeedSq > 0f ? math.lengthsq(vel - velocityReference) / maxSpeedSq : 0f;

        /// <summary>Normalized 0-1: 0 = on target, 1 = worst possible facing (π error).</summary>
        internal static float FacingCost(float yaw, float targetYaw, float width = 1f)
        {
            if (math.isnan(targetYaw)) return 0f;
            var err = math.abs(WrapRadians(yaw - targetYaw));
            var raw = err < width ? err * err : 2f * width * err - width * width;
            var maxRaw = 2f * width * math.PI - width * width;
            return raw / math.max(maxRaw, 1e-4f);
        }

        /// <summary>Normalized 0-1: 0 = no spin, 1 = at maxYawRate.</summary>
        internal static float YawRateCost(float yawRate, float maxYawRateSq) =>
            maxYawRateSq > 0f ? (yawRate * yawRate) / maxYawRateSq : 0f;

        /// <summary>Normalized 0-1: 0 = no input, 1 = all controls maxed.</summary>
        internal static float EffortCost(Control u) =>
            (u.thrust * u.thrust + u.strafe * u.strafe + u.yawTorque * u.yawTorque) / 3f;

        /// <summary>Normalized 0-1 per axis: 0 = no change, 1 = full reversal in one step.</summary>
        internal static float SmoothnessCost(Control u, Control prev, Config cfg)
        {
            // Max delta is 2 (-1 to +1), max rate = 2*invDt, max rate² = 4*invDt²
            var normFactor = 0.25f * cfg.dt * cfg.dt;
            var duT = u.thrust - prev.thrust;
            var duS = u.strafe - prev.strafe;
            var duY = u.yawTorque - prev.yawTorque;

            return (duT * duT * normFactor) * cfg.wSmoothnessThrust +
                   (duS * duS * normFactor) * cfg.wSmoothnessStrafe +
                   (duY * duY * normFactor) * cfg.wSmoothnessYaw;
        }

        /// <summary>Hard hull-overlap test between the (bank-narrowed, margin-inflated) ship disc and any obstacle disc. Near-binary by design: misses aren't penalized for proximity, so close-and-tight flying stays free (trade study §3.4).</summary>
        internal static bool Collides(float2 pos,
            Unity.Collections.NativeArray<ObstacleData> obstacles, int count, float hullRadius)
        {
            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var range = obs.radius + hullRadius;
                if (math.lengthsq(obs.position - pos) < range * range)
                    return true;
            }
            return false;
        }

        /// <summary>Collision-course-gated turn-away cost: only obstacles the velocity leads into and can't sidestep before impact cost anything (0 when the sidestep suffices, →1 as it falls short, C¹ at the boundary); worst obstacle wins. Chosen over the stopping-distance ratio after the A2 ablation (see Chase_Nav_Track_A_Implementation_Log.md).</summary>
        internal static float TurnAwayCost(float2 pos, float2 vel,
            Unity.Collections.NativeArray<ObstacleData> obstacles, int count,
            float hullRadius, float maxLatAccel)
        {
            var speed = math.length(vel);
            if (speed <= 1e-3f) return 0f;

            var velDir = vel / speed;
            var halfLatAccel = 0.5f * math.max(maxLatAccel, 1e-4f);
            var worst = 0f;

            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var toObs = obs.position - pos;
                var corridor = obs.radius + hullRadius;

                var along = math.dot(toObs, velDir);
                if (along <= 0f) continue;

                var perp = math.length(toObs - along * velDir);
                if (perp >= corridor) continue;

                var lateralClearanceNeeded = corridor - perp;
                var timeToObstaclePlane = along / speed;
                var maxSidestepBeforeImpact = halfLatAccel * timeToObstaclePlane * timeToObstaclePlane;
                var deficit = math.saturate(1f - maxSidestepBeforeImpact / math.max(lateralClearanceNeeded, 1e-4f));
                worst = math.max(worst, deficit * deficit);
            }
            return worst;
        }

        /// <summary>Penalizes velocity direction change vs the initial velocity: 0 maintaining course → 1 reversed; 0 when either velocity is near-zero.</summary>
        internal static float MomentumCost(float2 vel, float2 initialVel)
        {
            var speedSq = math.lengthsq(vel);
            var initSpeedSq = math.lengthsq(initialVel);
            if (speedSq < 1e-4f || initSpeedSq < 1e-4f) return 0f;

            var cosAngle = math.dot(vel, initialVel) / (math.sqrt(speedSq) * math.sqrt(initSpeedSq));
            return (1f - cosAngle) * 0.5f;
        }

        internal static float WrapRadians(float angle)
        {
            if (angle > math.PI) return angle - TwoPi;
            if (angle < -math.PI) return angle + TwoPi;
            return angle;
        }

#if UNITY_EDITOR
        public static CostBreakdown EvaluateBreakdown(State s, Control u, Control prevU,
            CostInput input, Config cfg, int step = 0)
        {
            var ctx = EvalContext.Create(s, input, cfg, step);
            var profileScale = BankProfileScale(u.strafe, cfg);

            // Shares Evaluate's obstacle resolution so the two can't drift.
            ObstacleCosts(s, input, cfg, profileScale, out var collision, out var obstacle);

            var breakdown = new CostBreakdown
            {
                velocityTrack = VelocityTrackCost(s.vel, input.velocityReference, cfg.maxSpeedSq) * cfg.wVelTrack,
                facing = FacingCost(s.yaw, ctx.facingTarget, cfg.facingWidth) * cfg.wFacing,
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

            // Mirrors Cost.Evaluate: the ramp applies to state cost (facing + regularizers); the tracker and control terms stay per-step.
            var stateCost = breakdown.facing + breakdown.yawRate + breakdown.obstacle + breakdown.momentum;
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
