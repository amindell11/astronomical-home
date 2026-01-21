using UnityEngine;

namespace AI.Steering.MPC
{
    /// <summary>
    /// Ship dynamics model for MPC trajectory prediction.
    /// </summary>
    public static class Model
    {
        public static State Step(State s, Control u, Config cfg)
        {
            var (fwd, right) = BodyAxes(s.yaw);
            var acc = ComputeAcceleration(u, fwd, right, cfg);
            var (nextPos, nextVel) = IntegrateLinear(s.pos, s.vel, acc, cfg.dt, cfg.maxSpeed);
            var (nextYaw, nextYawRate) = IntegrateAngular(s.yaw, s.yawRate, u.yawTorque, cfg);
            
            return new State { pos = nextPos, vel = nextVel, yaw = nextYaw, yawRate = nextYawRate };
        }

        private static (Vector2 fwd, Vector2 right) BodyAxes(float yaw)
        {
            var sin = Mathf.Sin(yaw);
            var cos = Mathf.Cos(yaw);
            return (new Vector2(-sin, cos), new Vector2(cos, sin));
        }

        private static Vector2 ComputeAcceleration(Control u, Vector2 fwd, Vector2 right, Config cfg)
        {
            var accF = (u.thrust >= 0 ? cfg.forwardAcc : cfg.reverseAcc) * u.thrust;
            var accS = cfg.strafeAcc * u.strafe;
            return fwd * accF + right * accS;
        }

        private static (Vector2 pos, Vector2 vel) IntegrateLinear(Vector2 pos, Vector2 vel, Vector2 acc, float dt, float maxSpeed)
        {
            var nextVel = vel + acc * dt;
            if (nextVel.sqrMagnitude > maxSpeed * maxSpeed)
                nextVel = nextVel.normalized * maxSpeed;
            return (pos + nextVel * dt, nextVel);
        }

        private static (float yaw, float yawRate) IntegrateAngular(float yaw, float yawRate, float torque, Config cfg)
        {
            var alpha = cfg.alphaMax * torque - cfg.damping * yawRate;
            var nextYawRate = Mathf.Clamp(yawRate + alpha * cfg.dt, -cfg.maxYawRate, cfg.maxYawRate);
            var nextYaw = WrapAngle(yaw + nextYawRate * cfg.dt);
            return (nextYaw, nextYawRate);
        }

        private static float WrapAngle(float angle)
        {
            while (angle > Mathf.PI) angle -= 2f * Mathf.PI;
            while (angle < -Mathf.PI) angle += 2f * Mathf.PI;
            return angle;
        }
    }
}
