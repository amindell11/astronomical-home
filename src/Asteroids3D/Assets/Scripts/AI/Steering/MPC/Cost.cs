using Unity.Collections;
using Unity.Mathematics;

namespace Movement.MPC
{
    public static partial class Cost
    {
        private const float ObstacleEpsilon = 0.01f;
        private const float HeadingGateDistance = 2f;
        private const float HeadingGateDistanceSq = HeadingGateDistance * HeadingGateDistance;
        private const float TwoPi = 2f * math.PI;

        /// <summary>
        /// Preprocessed per-step context shared by Evaluate and EvaluateBreakdown.
        /// Keeps goal-mode logic (Flee, arrival, heading flip) in one place.
        /// </summary>
        internal struct EvalContext
        {
            public float2 goalPos;
            public float enemyYaw;
            public bool hasEnemy;
            public bool isFlee;
            public float wVel;
            public float wYaw;
            public float2 headingGoal;
            public float facingTarget;

            internal static EvalContext Create(State s, CostInput input, Config cfg, int step)
            {
                var stepTime = step * cfg.dt;
                var hasEnemy = !math.isnan(input.enemyYaw);

                // Use pre-rolled enemy trajectory if available, otherwise linear extrapolation
                float2 goalPos;
                float2 goalVel;
                float enemyYaw;
                if (input.enemyStates.IsCreated && step < input.enemyStateCount)
                {
                    var es = input.enemyStates[step];
                    goalPos = es.pos;
                    goalVel = es.vel;
                    enemyYaw = hasEnemy ? es.yaw : input.enemyYaw;
                }
                else
                {
                    goalPos = input.goalPos + input.goalVel * stepTime;
                    goalVel = input.goalVel;
                    enemyYaw = hasEnemy ? input.enemyYaw + input.enemyYawRate * stepTime : input.enemyYaw;
                }

                var isFlee = cfg.goalMode == GoalMode.Flee;
                var wVel = cfg.wVel;
                var wYaw = cfg.wYaw;
                var distToGoalSq = math.lengthsq(goalPos - s.pos);

                // Arrival stabilization (disabled in Flee — we start near the goal and want to accelerate away)
                if (!isFlee && distToGoalSq < cfg.arrivalDistanceSq)
                {
                    var distToGoal = math.sqrt(distToGoalSq);
                    var t = 1f - (distToGoal / cfg.arrivalDistance);
                    wVel = math.lerp(cfg.wVel, cfg.wVel * cfg.arrivalVelScale, t);
                    wYaw = math.lerp(cfg.wYaw, cfg.wYaw * cfg.arrivalYawScale, t);
                }

                var facingTarget = cfg.facingTarget;
                if (input.projectileSpeed > 0f && hasEnemy)
                    facingTarget = InterceptYaw(s.pos, goalPos, goalVel, input.projectileSpeed);

                return new EvalContext
                {
                    goalPos = goalPos,
                    enemyYaw = enemyYaw,
                    hasEnemy = hasEnemy,
                    isFlee = isFlee,
                    wVel = wVel,
                    wYaw = wYaw,
                    headingGoal = isFlee ? 2f * s.pos - goalPos : goalPos,
                    facingTarget = facingTarget,
                };
            }
        }

        public static float Evaluate(State s, Control u, Control prevU,
            CostInput input, Config cfg, bool isTerminal, int step = 0)
        {
            var ctx = EvalContext.Create(s, input, cfg, step);

            var posCost = GoalCost(s.pos, ctx.goalPos, cfg) * cfg.wPos;
            var velCost = ctx.isFlee ? 0f : VelocityCost(s.vel) * ctx.wVel;
            var headingCost = HeadingCost(s.pos, s.yaw, ctx.headingGoal) * ctx.wYaw;
            var facingCost = FacingCost(s.yaw, ctx.facingTarget, cfg.facingWidth) * cfg.wFacing;
            var yawRateCost = YawRateCost(s.yawRate) * cfg.wYawRate;

            var obstacleCost = 0f;
            if (cfg.wObstacle > 0f && input.obstacleCount > 0)
            {
                var effectiveThreshold = cfg.obstacleThreshold + math.length(s.vel) * cfg.obstacleSpeedMargin;
                obstacleCost = ObstacleCost(s.pos, input.obstacles, input.obstacleCount, effectiveThreshold) * cfg.wObstacle;
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
                    losCost = LosCost(s.pos, ctx.goalPos, input.obstacles, input.obstacleCount) * cfg.wLos;
                if (cfg.wExposure > 0f)
                    exposureCost = ExposureCost(s.pos, ctx.goalPos, ctx.enemyYaw, cfg.exposureWidth) * cfg.wExposure;
                if (cfg.wTangential > 0f)
                    tangentialCost = TangentialVelocityCost(s.pos, s.vel, ctx.goalPos) * cfg.wTangential;
            }

            var positionalCost = posCost + velCost + headingCost + yawRateCost + obstacleCost + momentumCost;
            var tacticalCost = facingCost + losCost + exposureCost + tangentialCost;

            var effortCost = EffortCost(u) * cfg.wEffort;
            var boostEffortCost = u.boost * u.boost * cfg.wBoostEffort;
            var smoothnessCost = SmoothnessCost(u, prevU, cfg);
            var controlCost = effortCost + boostEffortCost + smoothnessCost;

            var total = positionalCost + tacticalCost + controlCost;

            if (isTerminal)
                total += cfg.terminalMultiplier * (positionalCost + tacticalCost);

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

        internal static float GoalCost(float2 pos, float2 goal, Config cfg)
        {
            switch (cfg.goalMode)
            {
                case GoalMode.MaintainRange:
                    return RangeBandCost(pos, goal, cfg.desiredRange, cfg.rangeTolerance);
                case GoalMode.Flee:
                    return FleeCost(pos, goal);
                default:
                    return PositionCost(pos, goal);
            }
        }

        internal static float PositionCost(float2 pos, float2 goal) => math.lengthsq(pos - goal);

        private const float FleeEpsilon = 1f;

        internal static float RangeBandCost(float2 pos, float2 goal, float desiredRange, float tolerance)
        {
            var dist = math.length(pos - goal);
            var err = math.abs(dist - desiredRange) - tolerance;
            if (err <= 0f) return 0f;
            return err * err;
        }

        internal static float FleeCost(float2 pos, float2 goal)
        {
            var dist = math.length(pos - goal);
            return FleeEpsilon / (dist + FleeEpsilon);
        }

        internal static float VelocityCost(float2 vel) => math.lengthsq(vel);

        internal static float HeadingCost(float2 pos, float yaw, float2 goal)
        {
            var toGoal = goal - pos;
            var distSq = math.lengthsq(toGoal);
            if (distSq < 1e-8f) return 0f;

            var goalYaw = math.atan2(-toGoal.x, toGoal.y);
            var angErr = WrapRadians(yaw - goalYaw);
            var cost = angErr * angErr;

            if (distSq >= HeadingGateDistanceSq) return cost;

            var normalizedDist = math.sqrt(distSq) / HeadingGateDistance;
            return cost * Smoothstep01(normalizedDist);
        }

        internal static float FacingCost(float yaw, float targetYaw, float width = 1f)
        {
            if (math.isnan(targetYaw)) return 0f;
            var err = math.abs(WrapRadians(yaw - targetYaw));
            return err < width ? err * err : 2f * width * err - width * width;
        }

        internal static float YawRateCost(float yawRate) => yawRate * yawRate;

        internal static float EffortCost(Control u) =>
            u.thrust * u.thrust + u.strafe * u.strafe + u.yawTorque * u.yawTorque;

        internal static float SmoothnessCost(Control u, Control prev, Config cfg)
        {
            var duT = (u.thrust - prev.thrust) * cfg.invDt;
            var duS = (u.strafe - prev.strafe) * cfg.invDt;
            var duY = (u.yawTorque - prev.yawTorque) * cfg.invDt;

            return (duT * duT) * cfg.wSmoothnessThrust +
                   (duS * duS) * cfg.wSmoothnessStrafe +
                   (duY * duY) * cfg.wSmoothnessYaw;
        }

        internal static float ObstacleCost(float2 pos, Unity.Collections.NativeArray<ObstacleData> obstacles,
            int count, float threshold)
        {
            if (count == 0) return 0f;

            var cost = 0f;
            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var range = obs.radius + threshold;
                var rangeSq = range * range;
                var distSq = math.lengthsq(pos - obs.position);

                if (distSq >= rangeSq) continue;

                var dist = math.sqrt(distSq);
                var norm = dist / range;
                cost += obs.weight / ((norm + ObstacleEpsilon) * (norm + ObstacleEpsilon));
            }
            return cost;
        }

        /// <summary>
        /// Penalizes positions where obstacles block the line from ship to enemy.
        /// Uses closest-approach distance to detect line-circle intersection.
        /// Returns a soft occlusion score: 0 = clear LOS, positive = blocked.
        /// </summary>
        public static float LosCost(float2 pos, float2 enemy,
            NativeArray<ObstacleData> obstacles, int count)
        {
            var ray = enemy - pos;
            var rayLenSq = math.lengthsq(ray);
            if (rayLenSq < 1e-8f) return 0f;

            var invRayLenSq = 1f / rayLenSq;
            var cost = 0f;

            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var toObs = obs.position - pos;

                // Project obstacle center onto ray, clamped to segment [0,1]
                var t = math.saturate(math.dot(toObs, ray) * invRayLenSq);
                var closest = pos + t * ray;
                var distSq = math.lengthsq(obs.position - closest);
                var rSq = obs.radius * obs.radius;

                if (distSq >= rSq) continue;

                // Smooth penetration: 1 at center, 0 at edge
                var penetration = 1f - math.sqrt(distSq) / obs.radius;
                cost += penetration * penetration;
            }

            return cost;
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
            var dist = math.length(toShip);
            if (dist < 1e-4f) return 1f; // On top of enemy = max exposure

            var dir = toShip / dist;
            // Enemy forward vector (matching yaw convention: yaw=0 → +Y)
            var enemyFwd = new float2(-math.sin(enemyYaw), math.cos(enemyYaw));
            var cosAngle = math.dot(enemyFwd, dir);

            // Angle from enemy's nose (0 = directly in front, π = behind)
            var angle = math.acos(math.clamp(cosAngle, -1f, 1f));
            var x = angle / math.max(width, 1e-4f);
            var angular = math.exp(-x * x);

            // Inverse-distance: at 1 unit → 1.0, at 10 units → 0.1, at 50 → 0.02
            var proximity = 1f / dist;
            return angular * proximity;
        }

        /// <summary>
        /// Rewards lateral (tangential) velocity relative to the enemy.
        /// High tangential speed = low cost, making the ship harder to track.
        /// </summary>
        internal static float TangentialVelocityCost(float2 pos, float2 vel, float2 enemyPos)
        {
            var toEnemy = enemyPos - pos;
            var dist = math.length(toEnemy);
            if (dist < 1e-4f) return 0f;
            var radialDir = toEnemy / dist;
            // Tangential speed = magnitude of velocity component perpendicular to radial direction
            var tangentialSpeed = math.abs(vel.x * radialDir.y - vel.y * radialDir.x);
            return 1f / (tangentialSpeed + 0.5f);
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
            return 1f - cosAngle; // 0 = same direction, 1 = perpendicular, 2 = opposite
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
