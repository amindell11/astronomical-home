using AI.Scanning;
using UnityEngine;

namespace Movement.MPC
{
    /// <summary>
    /// Cost evaluation for MPC trajectory optimization.
    /// </summary>
    public static partial class Cost
    {
        private const float ObstacleEpsilon = 0.01f;

        public static float Evaluate(State s, Control u, Control prevU, Vector2 goalPos, 
            ObstacleScan scan, Config cfg, bool isTerminal)
        {
            using var _ = EditorProfilingScope.Begin("MPC.Cost.Evaluate");
            var toGoal = goalPos - s.pos;
            var distToGoalSq = toGoal.sqrMagnitude;
            
            // Arrival stabilization
            var wVel = cfg.wVel;
            var wYaw = cfg.wYaw;
            
            if (distToGoalSq < cfg.arrivalDistanceSq)
            {
                var distToGoal = Mathf.Sqrt(distToGoalSq);
                var t = 1f - (distToGoal / cfg.arrivalDistance); // 0 at arrivalDist, 1 at goal
                wVel = Mathf.Lerp(cfg.wVel, cfg.wVel * cfg.arrivalVelScale, t);
                wYaw = Mathf.Lerp(cfg.wYaw, cfg.wYaw * cfg.arrivalYawScale, t);
            }

            var posCost = PositionCost(s.pos, goalPos) * cfg.wPos;
            var velCost = VelocityCost(s.vel) * wVel;
            var headingCost = HeadingCost(s.pos, s.yaw, goalPos) * wYaw;
            var facingCost = FacingCost(s.yaw, cfg.facingTarget) * cfg.wFacing;
            var yawRateCost = YawRateCost(s.yawRate) * cfg.wYawRate;
            var obstacleCost = 0f;
            if (cfg.wObstacle > 0f && scan.count > 0)
            {
                obstacleCost = ObstacleCost(s.pos, scan, cfg.obstacleThreshold) * cfg.wObstacle;
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


        private static float PositionCost(Vector2 pos, Vector2 goal) => (pos - goal).sqrMagnitude;

        private static float VelocityCost(Vector2 vel) => vel.sqrMagnitude;
        
        private static float HeadingCost(Vector2 pos, float yaw, Vector2 goal)
        {
            // 3. Heading cost (face the goal) - continuous + robust
            var toGoal = (goal - pos);
            var distSq = toGoal.sqrMagnitude;
            if (distSq < 1e-8f) return 0f;

            var dist = Mathf.Sqrt(distSq);
            var goalYaw = Mathf.Atan2(-toGoal.x, toGoal.y);
            var angErr = WrapRadians(yaw - goalYaw);
            var headingCost = angErr * angErr;

            // fade out heading near the goal so it doesn't dither at "arrived"
            var headingGate = Mathf.SmoothStep(0f, 1f, dist / 2f); // TODO add headingGateDist in cfg (e.g., 2f)
            headingCost *= headingGate;
            return headingCost;
        }

        private static float FacingCost(float yaw, float targetYaw)
        {
            if (float.IsNaN(targetYaw)) return 0f;
            
            var err = WrapRadians(yaw - targetYaw);
            return err * err;
        }

        private static float YawRateCost(float yawRate) => yawRate * yawRate;

        private static float EffortCost(Control u) =>
            u.thrust * u.thrust + u.strafe * u.strafe + u.yawTorque * u.yawTorque;

        private static float SmoothnessCost(Control u, Control prev, Config cfg)
        {
            var duT = (u.thrust - prev.thrust) * cfg.invDt;
            var duS = (u.strafe - prev.strafe) * cfg.invDt;
            var duY = (u.yawTorque - prev.yawTorque) * cfg.invDt;
            
            return (duT * duT) * cfg.wSmoothnessThrust + 
                   (duS * duS) * cfg.wSmoothnessStrafe + 
                   (duY * duY) * cfg.wSmoothnessYaw;
        }

        private static float ObstacleCost(Vector2 pos, ObstacleScan scan, float threshold)
        {
            if (scan.count == 0) return 0f;

            var cost = 0f;
            for (var i = 0; i < scan.count; i++)
            {
                var obs = scan.buffer[i];
                var range = obs.radius + threshold;
                var rangeSq = range * range;
                var distSq = (pos - obs.position).sqrMagnitude;

                if (distSq >= rangeSq) continue;
                
                var dist = Mathf.Sqrt(distSq);
                var norm = dist / range;
                cost += 1f / ((norm + ObstacleEpsilon) * (norm + ObstacleEpsilon));
            }
            return cost;
        }

        private static float WrapRadians(float angle)
        {
            while (angle > Mathf.PI) angle -= 2f * Mathf.PI;
            while (angle < -Mathf.PI) angle += 2f * Mathf.PI;
            return angle;
        }
    }
}
