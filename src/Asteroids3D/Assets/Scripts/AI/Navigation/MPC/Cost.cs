using Unity.Collections;
using Unity.Mathematics;

namespace Movement.MPC
{
    public static partial class Cost
    {
        private const float ObstacleEpsilonSq = 0.0001f;
        // Ranked obstacle buffer: two float4s indexed as a contiguous 8-element array.
        private struct RankedBuffer
        {
            private float4 a, b;
            public float this[int i]
            {
                get => i < 4 ? a[i] : b[i - 4];
                set { if (i < 4) a[i] = value; else b[i - 4] = value; }
            }
        }
        private const int MaxRankedObstacles = 8;
        private const float HeadingGateDistance = 2f;
        private const float HeadingGateDistanceSq = HeadingGateDistance * HeadingGateDistance;
        private const float TwoPi = 2f * math.PI;

        /// <summary>
        /// Preprocessed per-step context shared by Evaluate and EvaluateBreakdown.
        /// Keeps goal-mode logic (Flee, arrival, heading flip) in one place.
        /// </summary>
        internal struct EvalContext
        {
            public float2 goalTarget;   // positional/closing/heading goal (waypoint, or the enemy if anchored)
            public float2 enemyPos;     // tactical reference: predicted enemy position this step
            public float2 enemyVel;
            public float enemyYaw;
            public bool hasEnemy;
            public float wVel;
            public float wClosing;
            public float wYaw;
            public float2 headingGoal;
            public float facingTarget;

            internal static EvalContext Create(State s, CostInput input, Config cfg, int step)
            {
                var stepTime = step * cfg.dt;
                var hasEnemy = !math.isnan(input.enemyYaw);
                var anchored = cfg.goalMode.IsEnemyAnchored();

                // Predicted enemy this step: the pre-rolled trajectory if present, else linear
                // extrapolation from the snapshot. Drives the tactical costs, and the goal too
                // when it is enemy-anchored.
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

                // The navigation goal target is the enemy when the goal is anchored to it,
                // otherwise the (linearly extrapolated) absolute waypoint. This is the one
                // place "is the goal the enemy?" is resolved.
                var goalTarget = (anchored && hasEnemy)
                    ? enemyPos
                    : input.goalPos + input.goalVel * stepTime;

                var isFlee = cfg.goalMode == GoalMode.Flee;
                var wVel = cfg.wVel;
                var wYaw = cfg.wYaw;
                var distToGoalSq = math.lengthsq(goalTarget - s.pos);

                // Arrival stabilization (disabled in Flee — we start near the goal and want to accelerate away)
                if (!isFlee && distToGoalSq < cfg.arrivalDistanceSq)
                {
                    var distToGoal = math.sqrt(distToGoalSq);
                    var t = 1f - (distToGoal / cfg.arrivalDistance);
                    wVel = math.lerp(cfg.wVel, cfg.wVel * cfg.arrivalVelScale, t);
                    wYaw = math.lerp(cfg.wYaw, cfg.wYaw * cfg.arrivalYawScale, t);
                }

                // Flee disables both velocity damping and the closing-velocity reward.
                if (isFlee) wVel = 0f;
                var wClosing = isFlee ? 0f : cfg.wClosing;

                var facingTarget = cfg.facingTarget;
                if (input.projectileSpeed > 0f && hasEnemy)
                    facingTarget = InterceptYaw(s.pos, enemyPos, enemyVel, input.projectileSpeed);

                float2 headingGoal;
                if (isFlee) headingGoal = 2f * s.pos - goalTarget;
                else headingGoal = goalTarget;

                return new EvalContext
                {
                    goalTarget = goalTarget,
                    enemyPos = enemyPos,
                    enemyVel = enemyVel,
                    enemyYaw = enemyYaw,
                    hasEnemy = hasEnemy,
                    wVel = wVel,
                    wClosing = wClosing,
                    wYaw = wYaw,
                    headingGoal = headingGoal,
                    facingTarget = facingTarget,
                };
            }
        }

        public static float Evaluate(State s, Control u, Control prevU,
            CostInput input, Config cfg, bool isTerminal, int step = 0)
        {
            var ctx = EvalContext.Create(s, input, cfg, step);

            // Bank profile: cos(strafe * maxBank) gives the fraction of the ship's
            // cross-section visible from any horizontal direction. Used by obstacle
            // avoidance (narrower ship = tighter clearance) and miss-distance cost.
            var profileScale = cfg.maxBankAngleRad > 0f
                ? math.cos(math.abs(u.strafe) * cfg.maxBankAngleRad)
                : 1f;

            // Position + closing-velocity costs share the current goal. Flee/range-band
            // live inside PositionalGoalCost; the flee disables come baked into ctx weights.
            var posCost = PositionalGoalCost(s.pos, ctx, cfg) * cfg.wPos;
            var velCost = VelocityCost(s.vel, cfg.maxSpeedSq) * ctx.wVel;
            var closingCost = ctx.wClosing == 0f ? 0f
                : ClosingCost(s.pos, s.vel, ctx.goalTarget, cfg.maxSpeedSq, cfg.closingFadeDistance) * ctx.wClosing;
            var headingCost = HeadingCost(s.pos, s.yaw, ctx.headingGoal, cfg.wYawDistanceScale) * ctx.wYaw;
            var facingCost = FacingCost(s.yaw, ctx.facingTarget, cfg.facingWidth) * cfg.wFacing;
            var yawRateCost = YawRateCost(s.yawRate, cfg.maxYawRateSq) * cfg.wYawRate;

            var obstacleCost = 0f;
            if (cfg.wObstacle > 0f && input.obstacleCount > 0)
            {
                var baseThreshold = cfg.obstacleThreshold + math.length(s.vel) * cfg.obstacleSpeedMargin;
                var effectiveThreshold = baseThreshold * profileScale;
                obstacleCost = ObstacleCost(s.pos, s.vel, input.obstacles, input.obstacleCount,
                    effectiveThreshold, cfg.obstacleFalloffCurve,
                    cfg.obstacleClosingScale, cfg.obstacleClosingHalfSpeed) * cfg.wObstacle;
            }

            var momentumCost = 0f;
            if (cfg.wMomentum > 0f)
                momentumCost = MomentumCost(s.vel, input.initialVel) * cfg.wMomentum;

            var losCost = 0f;
            var exposureCost = 0f;
            var tangentialCost = 0f;
            if (ctx.hasEnemy)
            {
                if (cfg.wLos > 0f && input.obstacleCount > 0)
                    losCost = LosCost(s.pos, ctx.enemyPos, input.obstacles, input.obstacleCount) * cfg.wLos;
                if (cfg.wExposure > 0f)
                    exposureCost = ExposureCost(s.pos, ctx.enemyPos, ctx.enemyYaw, cfg.exposureWidth) * cfg.wExposure;
                if (cfg.wTangential > 0f)
                    tangentialCost = TangentialVelocityCost(s.pos, s.vel, ctx.enemyPos) * cfg.wTangential;
            }

            var missDistanceCost = 0f;
            if (cfg.wMissDistance > 0f && input.projectileSpeed > 0f && ctx.hasEnemy)
                missDistanceCost = MissDistanceCost(s.pos, s.vel, ctx.enemyPos, input.projectileSpeed,
                    profileScale) * cfg.wMissDistance;

            var positionalCost = posCost + velCost + closingCost + headingCost + yawRateCost + obstacleCost + momentumCost;
            var tacticalCost = facingCost + losCost + exposureCost + tangentialCost + missDistanceCost;

            var effortCost = EffortCost(u) * cfg.wEffort;
            var boostEffortCost = u.boost * u.boost * cfg.wBoostEffort;
            var smoothnessCost = SmoothnessCost(u, prevU, cfg);
            var controlCost = effortCost + boostEffortCost + smoothnessCost;

            var total = positionalCost + tacticalCost + controlCost;

            if (cfg.terminalMultiplier > 0f && cfg.horizon > 1)
            {
                var t = step / (float)(cfg.horizon - 1);
                var ramp = math.pow(t, cfg.terminalCurve) * cfg.terminalMultiplier;
                total += ramp * (positionalCost + tacticalCost);
            }

            return total;
        }

        /// <summary>
        /// Computes the yaw angle to aim at a first-order intercept point.
        /// Uses t = dist / projectileSpeed as time-of-flight estimate.
        /// </summary>
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

        /// <summary>
        /// Single resolution point for the position cost target. Shared by Evaluate and
        /// EvaluateBreakdown so goal-mode handling lives in exactly one place.
        /// </summary>
        internal static float PositionalGoalCost(float2 pos, in EvalContext ctx, in Config cfg)
            => GoalCost(pos, ctx.goalTarget, cfg);

        internal static float GoalCost(float2 pos, float2 goal, Config cfg)
        {
            switch (cfg.goalMode)
            {
                case GoalMode.MaintainRange:
                    return RangeBandCost(pos, goal, cfg.desiredRange, cfg.rangeTolerance, cfg.positionCurve);
                case GoalMode.Flee:
                    return FleeCost(pos, goal, cfg.positionCurve);
                default:
                    return PositionCost(pos, goal, cfg.positionCurve, cfg.positionSaturationDistance);
            }
        }

        /// <summary>
        /// Position cost normalized to [0, 1) via Lorentzian saturation: cost = raw / (raw + satMax),
        /// where raw = d^curve and satMax = satDistance^curve. Half-saturates at d = satDistance,
        /// asymptotes to 1 at far distance. Closing-velocity reward provides the long-range
        /// urgency that an unbounded quadratic used to. satDistance &lt;= 0 falls back to raw d^curve
        /// (unbounded; legacy behavior).
        /// </summary>
        internal static float PositionCost(float2 pos, float2 goal, float curve, float satDistance)
        {
            var raw = (curve == 2f)
                ? math.lengthsq(pos - goal)
                : math.pow(math.length(pos - goal), curve);
            if (satDistance <= 0f) return raw;
            var satMax = math.pow(satDistance, curve);
            return raw / (raw + satMax);
        }

        private const float FleeEpsilon = 1f;

        internal static float RangeBandCost(float2 pos, float2 goal, float desiredRange, float tolerance, float curve)
        {
            var dist = math.length(pos - goal);
            var inner = desiredRange - tolerance;
            var outer = desiredRange + tolerance;

            if (dist >= inner && dist <= outer) return 0f;

            if (dist > outer)
            {
                // Too far: Lorentzian-saturated urgency normalized to [0, 1).
                // Half-saturates at err = tolerance, asymptotes to 1.
                var err = dist - outer;
                var errSq = err * err;
                var tolSq = math.max(tolerance * tolerance, 1e-4f);
                return errSq / (errSq + tolSq);
            }

            // Too close: diminishing reward — closer is better for aiming but with soft floor
            // Returns negative (reward), approaching -1 as dist→0
            var t = dist / math.max(inner, 1e-4f); // 1 at inner edge, 0 at enemy
            return -(1f - t * t);
        }

        internal static float FleeCost(float2 pos, float2 goal, float curve)
        {
            var dist = math.length(pos - goal);
            return FleeEpsilon / math.pow(dist + FleeEpsilon, curve * 0.5f);
        }

        /// <summary>Normalized 0-1: 0 = stopped, 1 = at maxSpeed.</summary>
        internal static float VelocityCost(float2 vel, float maxSpeedSq) =>
            maxSpeedSq > 0f ? math.lengthsq(vel) / maxSpeedSq : 0f;

        /// <summary>
        /// Negative-when-closing reward for velocity component aimed at goal. Provides a
        /// Lyapunov-style gradient so "spin slightly slower with slight thrust" beats
        /// "spin at full rate" — escape path from sample-based MPC's spinning local optima.
        /// Returns ~ [-1, 1]: -1 = closing at maxSpeed, +1 = receding at maxSpeed.
        /// Smoothstep-gated to 0 within fadeDistance so velocity-damping arrival can take over.
        /// </summary>
        internal static float ClosingCost(float2 pos, float2 vel, float2 goal,
            float maxSpeedSq, float fadeDistance)
        {
            var toGoal = goal - pos;
            var distSq = math.lengthsq(toGoal);
            if (distSq < 1e-8f || maxSpeedSq <= 0f) return 0f;

            var dist = math.sqrt(distSq);
            var closingSpeed = math.dot(vel, toGoal / dist);
            var raw = -closingSpeed / math.sqrt(maxSpeedSq);

            if (fadeDistance <= 0f) return raw;
            var gate = Smoothstep01(dist / fadeDistance);
            return raw * gate;
        }

        /// <summary>Normalized 0-1 (scaled by 1 + distanceScale*dist): 0 = pointing at goal, large when misaligned at range.</summary>
        internal static float HeadingCost(float2 pos, float yaw, float2 goal, float distanceScale)
        {
            var toGoal = goal - pos;
            var distSq = math.lengthsq(toGoal);
            if (distSq < 1e-8f) return 0f;

            var goalYaw = math.atan2(-toGoal.x, toGoal.y);
            var angErr = WrapRadians(yaw - goalYaw);
            var cost = (angErr * angErr) / (math.PI * math.PI);

            // Scale by (1 + scale * dist) so heading stays visible vs position cost (≈ dist²) at range.
            // Matches ∂PositionCost/∂heading for positionCurve=2; scale=0 disables and recovers normalized 0-1.
            var dist = math.sqrt(distSq);
            var distMultiplier = 1f + distanceScale * dist;

            if (distSq >= HeadingGateDistanceSq) return cost * distMultiplier;

            var normalizedDist = dist / HeadingGateDistance;
            return cost * Smoothstep01(normalizedDist) * distMultiplier;
        }

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
            var normFactor = 0.25f * cfg.dt * cfg.dt; // 1 / (4 * invDt²)
            var duT = u.thrust - prev.thrust;
            var duS = u.strafe - prev.strafe;
            var duY = u.yawTorque - prev.yawTorque;

            return (duT * duT * normFactor) * cfg.wSmoothnessThrust +
                   (duS * duS * normFactor) * cfg.wSmoothnessStrafe +
                   (duY * duY * normFactor) * cfg.wSmoothnessYaw;
        }

        internal static float ObstacleCost(float2 pos, float2 vel,
            Unity.Collections.NativeArray<ObstacleData> obstacles,
            int count, float threshold, float falloffCurve,
            float closingScale, float closingHalfSpeed)
        {
            if (count == 0) return 0f;

            var halfCurve = falloffCurve * 0.5f;
            var ranked = new RankedBuffer();
            var rankedCount = 0;
            // Saturating closing-speed multiplier: c *= 1 + scale * v / (v + halfSpeed).
            // Bounded growth (asymptote = 1 + scale) prevents the optimizer from chasing
            // arbitrarily large gains by trimming thrust near obstacles.
            var closingActive = closingScale > 0f && closingHalfSpeed > 0f;

            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var range = obs.radius + threshold;
                var rangeSq = range * range;
                var toObs = obs.position - pos;
                var distSq = math.lengthsq(toObs);

                if (distSq >= rangeSq) continue;

                var normSq = distSq / rangeSq;
                // Normalize so cost ≈ weight at threshold edge (normSq≈1), >weight closer to surface
                var c = obs.weight * math.pow(1f + ObstacleEpsilonSq, halfCurve) /
                        math.pow(normSq + ObstacleEpsilonSq, halfCurve);

                if (closingActive && distSq > 1e-8f)
                {
                    var dirToObs = toObs * math.rsqrt(distSq);
                    var closingSpeed = math.max(0f, math.dot(vel, dirToObs));
                    c *= 1f + closingScale * closingSpeed / (closingSpeed + closingHalfSpeed);
                }

                // Insert into descending sorted buffer of 8
                if (rankedCount < MaxRankedObstacles)
                {
                    ranked[rankedCount] = c;
                    for (var j = rankedCount; j > 0 && ranked[j] > ranked[j - 1]; j--)
                        (ranked[j], ranked[j - 1]) = (ranked[j - 1], ranked[j]);
                    rankedCount++;
                }
                else if (c > ranked[MaxRankedObstacles - 1])
                {
                    ranked[MaxRankedObstacles - 1] = c;
                    for (var j = MaxRankedObstacles - 1; j > 0 && ranked[j] > ranked[j - 1]; j--)
                        (ranked[j], ranked[j - 1]) = (ranked[j - 1], ranked[j]);
                }
            }

            // Harmonic-weighted sum, then Lorentzian-normalized to [0, 1).
            // Per-obstacle c can be unbounded inside the threshold (raw inverse-power blows up
            // near surface), so a simple total/harmonicMax was *only* normalized at the edge case.
            // Lorentzian saturation bounds the result regardless of N obstacles or depth:
            //   N=1 at edge → ~0.27, N=8 at edge → 0.5, any depth → asymptotes to 1.
            // Harmonic(8) = 1 + 1/2 + 1/3 + ... + 1/8 ≈ 2.717
            const float harmonicMax = 2.717f;
            var total = 0f;
            for (var i = 0; i < rankedCount; i++)
                total += ranked[i] / (i + 1);

            return total / (total + harmonicMax);
        }

        /// <summary>
        /// Penalizes positions where obstacles block the line from ship to enemy.
        /// Uses closest-approach distance to detect line-circle intersection.
        /// Returns a soft occlusion score: 0 = clear LOS, positive = blocked.
        /// </summary>
        /// <summary>Normalized 0-1: 0 = clear LOS, 1 = fully blocked.</summary>
        public static float LosCost(float2 pos, float2 enemy,
            NativeArray<ObstacleData> obstacles, int count)
        {
            var ray = enemy - pos;
            var rayLenSq = math.lengthsq(ray);
            if (rayLenSq < 1e-8f) return 0f;

            var invRayLenSq = 1f / rayLenSq;
            var maxPenetration = 0f;

            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var rSq = obs.radius * obs.radius;

                // Skip the target itself: if the goal endpoint sits inside this obstacle's sphere,
                // the obstacle IS the target (or wraps it) — no self-blocking of LOS to the very thing we're aiming at.
                if (math.lengthsq(obs.position - enemy) <= rSq) continue;

                var toObs = obs.position - pos;
                var t = math.saturate(math.dot(toObs, ray) * invRayLenSq);
                var closest = pos + t * ray;
                var distSq = math.lengthsq(obs.position - closest);

                if (distSq >= rSq) continue;

                var penetration = 1f - math.sqrt(distSq) / obs.radius;
                maxPenetration = math.max(maxPenetration, penetration * penetration);
            }

            return maxPenetration;
        }

        /// <summary>
        /// Penalizes being in the enemy's forward weapon arc.
        /// Cost is highest when directly in front of the enemy at close range,
        /// zero when behind/beside. Inverse-distance scaling makes close-range
        /// exposure far more urgent than long-range.
        /// </summary>
        public static float ExposureCost(float2 pos, float2 enemyPos, float enemyYaw,
            float width = 1f)
        {
            var toShip = pos - enemyPos;
            var distSq = math.lengthsq(toShip);
            if (distSq < 1e-8f) return 1f; // On top of enemy = max exposure

            var dir = toShip * math.rsqrt(distSq);
            // Enemy forward vector (matching yaw convention: yaw=0 → +Y)
            var enemyFwd = new float2(-math.sin(enemyYaw), math.cos(enemyYaw));
            var cosAngle = math.dot(enemyFwd, dir);

            // Angle from enemy's nose (0 = directly in front, π = behind)
            var angle = math.acos(math.clamp(cosAngle, -1f, 1f));
            var x = angle / math.max(width, 1e-4f);
            return math.exp(-x * x);
        }

        /// <summary>
        /// Rewards lateral (tangential) velocity relative to the enemy.
        /// High tangential speed = low cost, making the ship harder to track.
        /// </summary>
        /// <summary>Normalized 0-1: 1 = no lateral movement, ~0 = fast lateral movement.</summary>
        internal static float TangentialVelocityCost(float2 pos, float2 vel, float2 enemyPos)
        {
            var toEnemy = enemyPos - pos;
            var dist = math.length(toEnemy);
            if (dist < 1e-4f) return 0f;
            var radialDir = toEnemy / dist;
            var tangentialSpeed = math.abs(vel.x * radialDir.y - vel.y * radialDir.x);
            return 0.5f / (tangentialSpeed + 0.5f);
        }

        /// <summary>
        /// Penalizes states where the ship is easy to hit.
        /// Computes miss distance: the perpendicular displacement of the ship
        /// from the enemy's line of fire during the projectile's time of flight.
        /// Banking (from strafe) reduces the ship's cross-section by cos(bankAngle),
        /// shrinking the effective profile the enemy must hit.
        /// Naturally captures speed, lateral movement, range, and bank in one term.
        /// </summary>
        internal static float MissDistanceCost(float2 pos, float2 vel, float2 enemyPos,
            float projectileSpeed, float profileScale)
        {
            var toShip = pos - enemyPos;
            var distSq = math.lengthsq(toShip);
            if (distSq < 1e-4f) return 1f; // On top of enemy = max danger

            var dist = math.sqrt(distSq);
            var tof = dist / projectileSpeed;

            // Perpendicular velocity component relative to the line of fire
            var radialDir = toShip / dist;
            var radialSpeed = math.dot(vel, radialDir);
            var tangentialSpeedSq = math.lengthsq(vel) - radialSpeed * radialSpeed;
            var missDistance = math.sqrt(math.max(0f, tangentialSpeedSq)) * tof;

            // Banking shrinks the ship's profile: effective width = baseWidth * cos(bankAngle)
            var effectiveProfile = 0.5f * profileScale;
            return effectiveProfile / (missDistance + effectiveProfile);
        }

        /// <summary>
        /// Penalizes velocity direction changes relative to the initial velocity.
        /// Returns 0 when maintaining course, up to 2 when reversing direction.
        /// Returns 0 when either velocity is near-zero (no meaningful direction).
        /// </summary>
        internal static float MomentumCost(float2 vel, float2 initialVel)
        {
            var speedSq = math.lengthsq(vel);
            var initSpeedSq = math.lengthsq(initialVel);
            if (speedSq < 1e-4f || initSpeedSq < 1e-4f) return 0f;

            var cosAngle = math.dot(vel, initialVel) / (math.sqrt(speedSq) * math.sqrt(initSpeedSq));
            return (1f - cosAngle) * 0.5f; // 0 = same direction, 0.5 = perpendicular, 1 = opposite
        }

        internal static float WrapRadians(float angle)
        {
            if (angle > math.PI) return angle - TwoPi;
            if (angle < -math.PI) return angle + TwoPi;
            return angle;
        }

        private static float Smoothstep01(float x)
        {
            x = math.clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }
}
