using UnityEngine;

namespace AI.Steering.MPC
{
    /// <summary>
    /// Cost evaluation for MPC trajectory optimization.
    /// </summary>
    public static class Cost
    {
        public static float Evaluate(State s, Control u, Control prevU, Vector2 goalPos, 
            Scanning.DetectedObstacle[] obstacles, int obstacleCount, Config cfg, bool isTerminal)
        {
            var total = PositionCost(s.pos, goalPos) * cfg.wPos
                      + VelocityCost(s.vel) * cfg.wVel
                      + HeadingCost(s.pos, s.yaw, goalPos) * cfg.wYaw
                      + YawRateCost(s.yawRate) * cfg.wYawRate
                      + EffortCost(u) * cfg.wEffort
                      + SmoothnessCost(u, prevU) * cfg.wSmoothness
                      + ObstacleCost(s.pos, obstacles, obstacleCount, cfg.obstacleThreshold) * cfg.wObstacle;

            return isTerminal ? total * cfg.terminalMultiplier : total;
        }

        private static float PositionCost(Vector2 pos, Vector2 goal) => (pos - goal).sqrMagnitude;

        private static float VelocityCost(Vector2 vel) => vel.sqrMagnitude;
        
        private static float HeadingCost(Vector2 pos, float yaw, Vector2 goal)
        {
            // 3. Heading cost (face the goal) - continuous + robust
            var toGoal = (goal - pos);
            var dist = toGoal.magnitude;
            var dirToGoal = toGoal / (dist + 1e-4f);          // avoids noisy normalize
            var fwd = new Vector2(-Mathf.Sin(yaw), Mathf.Cos(yaw));

            var dot = Mathf.Clamp(Vector2.Dot(fwd, dirToGoal), -1f, 1f);
            var cross = fwd.x * dirToGoal.y - fwd.y * dirToGoal.x;
            var angErr = Mathf.Atan2(cross, dot);          // [-pi, pi]
            var headingCost = angErr * angErr;

            // fade out heading near the goal so it doesn't dither at "arrived"
            var headingGate = Mathf.SmoothStep(0f, 1f, dist / 2f); // TODO add headingGateDist in cfg (e.g., 2f)
            headingCost *= headingGate;
            return headingCost;
        }

        private static float YawRateCost(float yawRate) => yawRate * yawRate;

        private static float EffortCost(Control u) =>
            u.thrust * u.thrust + u.strafe * u.strafe + u.yawTorque * u.yawTorque;

        private static float SmoothnessCost(Control u, Control prev)
        {
            var dt = u.thrust - prev.thrust;
            var ds = u.strafe - prev.strafe;
            var dy = u.yawTorque - prev.yawTorque;
            return dt * dt + ds * ds + dy * dy;
        }

        private static float ObstacleCost(Vector2 pos, Scanning.DetectedObstacle[] obstacles, int count, float threshold)
        {
            if (obstacles == null || count == 0) return 0f;

            var cost = 0f;
            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var dist = Vector2.Distance(pos, obs.Position);
                var range = obs.Radius + threshold;

                if (dist >= range) continue;
                
                var norm = dist / range;
                cost += 1f / ((norm + 0.01f) * (norm + 0.01f));
            }
            return cost;
        }
    }
}
