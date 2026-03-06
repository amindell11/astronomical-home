using UnityEngine;

namespace Movement.MPC
{
    /// <summary>
    /// Ship dynamics model for MPC trajectory prediction.
    /// </summary>
    public static partial class Model
    {
        public static State Step(State s, Control u, Config cfg, Dynamics shp)
        {
            using var _ = EditorProfilingScope.Begin("MPC.Model.Step");
            var (fwd, right) = BodyAxes(s.yaw);
            var acc = ComputeAcceleration(u, fwd, right, cfg, shp, s.vel);
            var (nextPos, nextVel) = IntegrateLinear(s.pos, s.vel, acc, cfg.dt, shp);
            var (nextYaw, nextYawRate) = IntegrateAngular(s.yaw, s.yawRate, u.yawTorque, cfg, shp);
            
            return new State { pos = nextPos, vel = nextVel, yaw = nextYaw, yawRate = nextYawRate };
        }

        private static (Vector2 fwd, Vector2 right) BodyAxes(float yaw)
        {
            var sin = Mathf.Sin(yaw);
            var cos = Mathf.Cos(yaw);
            return (new Vector2(-sin, cos), new Vector2(cos, sin));
        }

        private static Vector2 ComputeAcceleration(Control u, Vector2 fwd, Vector2 right, Config cfg, Dynamics shp, Vector2 vel)
        {
            var accF = ((u.thrust >= 0 ? shp.forwardAcc : shp.reverseAcc) * u.thrust) / shp.mass;
            
            var speedPct = Mathf.Clamp01(vel.magnitude / shp.maxSpeed);
            var strafeMag = Mathf.Lerp(shp.maxStrafeAcc, shp.minStrafeAcc, speedPct);
            var accS = (strafeMag * u.strafe) / shp.mass;
            
            return fwd * accF + right * accS;
        }

        private static (Vector2 pos, Vector2 vel) IntegrateLinear(Vector2 pos, Vector2 vel, Vector2 acc, float dt, Dynamics shp)
        {
            var dragFactor = Mathf.Max(0f, 1f - shp.linearDrag * dt);
            var nextVel = vel * dragFactor + acc * dt;
            if (nextVel.sqrMagnitude > shp.maxSpeed * shp.maxSpeed)
                nextVel = nextVel.normalized * shp.maxSpeed;
            return (pos + nextVel * dt, nextVel);
        }

        private static (float yaw, float yawRate) IntegrateAngular(float yaw, float yawRate, float yawInput, Config cfg, Dynamics shp)
        {
            var torqueAlpha = shp.yawTorque * yawInput;
            var dragFactor = Mathf.Max(0f, 1f - shp.angularDrag * cfg.dt);
            var nextYawRate = yawRate * dragFactor + torqueAlpha * cfg.dt;
            nextYawRate = Mathf.Clamp(nextYawRate, -shp.maxYawRate, shp.maxYawRate);
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
