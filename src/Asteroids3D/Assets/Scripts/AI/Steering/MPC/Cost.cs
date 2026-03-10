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
            CostInput input, Config cfg, bool isTerminal)
        {
            var toGoal = input.goalPos - s.pos;
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

            var posCost = PositionCost(s.pos, input.goalPos) * cfg.wPos;
            var velCost = VelocityCost(s.vel) * wVel;
            var hasFacing = !math.isnan(cfg.facingTarget);
            var headingCost = hasFacing ? 0f : HeadingCost(s.pos, s.yaw, input.goalPos) * wYaw;
            var facingCost = FacingCost(s.yaw, cfg.facingTarget) * cfg.wFacing;
            var yawRateCost = YawRateCost(s.yawRate) * cfg.wYawRate;
            var obstacleCost = 0f;
            if (cfg.wObstacle > 0f && input.obstacleCount > 0)
            {
                obstacleCost = ObstacleCost(s.pos, input.obstacles, input.obstacleCount, cfg.obstacleThreshold) * cfg.wObstacle;
            }

            var stateCost = posCost + velCost + headingCost + facingCost + yawRateCost + obstacleCost;

            var effortCost = EffortCost(u) * cfg.wEffort;
            var smoothnessCost = SmoothnessCost(u, prevU, cfg);
            var controlCost = effortCost + smoothnessCost;

            var total = stateCost + controlCost;

            if (isTerminal)
                total += cfg.terminalMultiplier * stateCost;

            return total;
        }

        internal static float PositionCost(float2 pos, float2 goal) => math.lengthsq(pos - goal);

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

        internal static float FacingCost(float yaw, float targetYaw)
        {
            if (math.isnan(targetYaw)) return 0f;
            var err = WrapRadians(yaw - targetYaw);
            return err * err;
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
