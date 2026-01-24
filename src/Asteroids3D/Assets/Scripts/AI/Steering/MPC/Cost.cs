using AI.Scanning;
using UnityEngine;

namespace AI.Steering.MPC
{
    /// <summary>
    /// Cost evaluation for MPC trajectory optimization.
    /// </summary>
    public static partial class Cost
    {
        public static float Evaluate(State s, Control u, Control prevU, Vector2 goalPos, 
            ObstacleScan scan, Config cfg, bool isTerminal)
        {
            var posCost = PositionCost(s.pos, goalPos) * cfg.wPos;
            var velCost = VelocityCost(s.vel) * cfg.wVel;
            var headingCost = HeadingCost(s.pos, s.yaw, goalPos) * cfg.wYaw;
            var facingCost = FacingCost(s.yaw, cfg.facingTarget) * cfg.wFacing;
            var yawRateCost = YawRateCost(s.yawRate) * cfg.wYawRate;
            var obstacleCost = ObstacleCost(s.pos, scan, cfg.obstacleThreshold) * cfg.wObstacle;

            var stateCost = posCost + velCost + headingCost + facingCost + yawRateCost + obstacleCost;

            var effortCost = EffortCost(u) * cfg.wEffort;
            var smoothnessCost = SmoothnessCost(u, prevU, cfg.dt) * cfg.wSmoothness;
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

        private static float FacingCost(float yaw, float targetYaw)
        {
            if (float.IsNaN(targetYaw)) return 0f;
            
            var err = yaw - targetYaw;
            while (err > Mathf.PI) err -= 2f * Mathf.PI;
            while (err < -Mathf.PI) err += 2f * Mathf.PI;
            return err * err;
        }

        private static float YawRateCost(float yawRate) => yawRate * yawRate;

        private static float EffortCost(Control u) =>
            u.thrust * u.thrust + u.strafe * u.strafe + u.yawTorque * u.yawTorque;

        private static float SmoothnessCost(Control u, Control prev, float dt)
        {
            // Rate of change (divide by dt), scaled down by 100 to compensate
            var scale = 1f / (dt * 100f);
            var duT = (u.thrust - prev.thrust) * scale;
            var duS = (u.strafe - prev.strafe) * scale;
            var duY = (u.yawTorque - prev.yawTorque) * scale;
            return duT * duT + duS * duS + duY * duY;
        }

        private static float ObstacleCost(Vector2 pos, ObstacleScan scan, float threshold)
        {
            var cost = 0f;
            for (var i = 0; i < scan.count; i++)
            {
                var obs = scan.buffer[i];
                var dist = Vector2.Distance(pos, obs.position);
                var range = obs.radius + threshold;

                if (dist >= range) continue;
                
                var norm = dist / range;
                cost += 1f / ((norm + 0.01f) * (norm + 0.01f));
            }
            return cost;
        }
    }
}
