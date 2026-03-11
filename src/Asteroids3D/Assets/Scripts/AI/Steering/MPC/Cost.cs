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

        public static float Evaluate(State s, Control u, Control prevU,
            CostInput input, Config cfg, bool isTerminal, int step = 0)
        {
            // Project enemy state forward for this timestep
            var stepTime = step * cfg.dt;
            var goalPos = input.goalPos + input.goalVel * stepTime;
            var hasEnemy = !math.isnan(input.enemyYaw);
            var enemyYaw = hasEnemy ? input.enemyYaw + input.enemyYawRate * stepTime : input.enemyYaw;

            var toGoal = goalPos - s.pos;
            var distToGoalSq = math.lengthsq(toGoal);

            // Arrival stabilization
            var wVel = cfg.wVel;
            var wYaw = cfg.wYaw;

            if (distToGoalSq < cfg.arrivalDistanceSq)
            {
                var distToGoal = math.sqrt(distToGoalSq);
                var t = 1f - (distToGoal / cfg.arrivalDistance);
                wVel = math.lerp(cfg.wVel, cfg.wVel * cfg.arrivalVelScale, t);
                wYaw = math.lerp(cfg.wYaw, cfg.wYaw * cfg.arrivalYawScale, t);
            }

            var posCost = GoalCost(s.pos, goalPos, cfg) * cfg.wPos;
            var velCost = VelocityCost(s.vel) * wVel;
            var headingCost = HeadingCost(s.pos, s.yaw, goalPos) * wYaw;

            // Dynamic facing: compute lead-target angle per step, or fall back to static override
            var facingTarget = cfg.facingTarget;
            if (input.projectileSpeed > 0f && hasEnemy)
                facingTarget = InterceptYaw(s.pos, goalPos, input.goalVel, input.projectileSpeed);
            var facingCost = FacingCost(s.yaw, facingTarget, cfg.facingPower) * cfg.wFacing;

            var yawRateCost = YawRateCost(s.yawRate) * cfg.wYawRate;
            var obstacleCost = 0f;
            if (cfg.wObstacle > 0f && input.obstacleCount > 0)
            {
                obstacleCost = ObstacleCost(s.pos, input.obstacles, input.obstacleCount, cfg.obstacleThreshold) * cfg.wObstacle;
            }

            var losCost = 0f;
            var exposureCost = 0f;
            var tangentialCost = 0f;
            if (hasEnemy)
            {
                if (cfg.wLos > 0f && input.obstacleCount > 0)
                    losCost = LosCost(s.pos, goalPos, input.obstacles, input.obstacleCount) * cfg.wLos;
                if (cfg.wExposure > 0f)
                    exposureCost = ExposureCost(s.pos, goalPos, enemyYaw, cfg.exposurePower) * cfg.wExposure;
                if (cfg.wTangential > 0f)
                    tangentialCost = TangentialVelocityCost(s.pos, s.vel, goalPos) * cfg.wTangential;
            }

            // Positional costs benefit from terminal boost (planning toward good end state)
            var positionalCost = posCost + velCost + headingCost + yawRateCost + obstacleCost;
            // Tactical costs are projected forward and can now be terminal-boosted
            var tacticalCost = facingCost + losCost + exposureCost + tangentialCost;

            var effortCost = EffortCost(u) * cfg.wEffort;
            var smoothnessCost = SmoothnessCost(u, prevU, cfg);
            var controlCost = effortCost + smoothnessCost;

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
            var distSq = math.lengthsq(pos - goal);
            return FleeEpsilon / (distSq + FleeEpsilon);
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

        internal static float FacingCost(float yaw, float targetYaw, float power = 1f)
        {
            if (math.isnan(targetYaw)) return 0f;
            var err = math.abs(WrapRadians(yaw - targetYaw));
            return math.pow(err, 1f / power);
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
            float power = 2f)
        {
            var toShip = pos - enemyPos;
            var dist = math.length(toShip);
            if (dist < 1e-4f) return 1f; // On top of enemy = max exposure

            var dir = toShip / dist;
            // Enemy forward vector (matching yaw convention: yaw=0 → +Y)
            var enemyFwd = new float2(-math.sin(enemyYaw), math.cos(enemyYaw));
            var cosAngle = math.dot(enemyFwd, dir);

            // Only penalize being in front half (cosAngle > 0)
            if (cosAngle <= 0f) return 0f;

            var angular = math.pow(cosAngle, power);
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
