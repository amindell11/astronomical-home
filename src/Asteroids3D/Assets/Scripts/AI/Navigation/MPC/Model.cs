using Unity.Mathematics;
using Movement;

namespace Movement.MPC
{
    public static class Model
    {
        public static State Step(State s, Control u, Config cfg, Dynamics shp)
        {
            BodyAxes(s.yaw, out var fwd, out var right);
            var acc = ComputeAcceleration(u, fwd, right, shp, s.vel);
            IntegrateLinear(s.pos, s.vel, acc, cfg.dt, shp, out var nextPos, out var nextVel);
            IntegrateAngular(s.yaw, s.yawRate, u.yawTorque, u.strafe, cfg, shp, out var nextYaw, out var nextYawRate);

            return new State { pos = nextPos, vel = nextVel, yaw = nextYaw, yawRate = nextYawRate };
        }

        private static void BodyAxes(float yaw, out float2 fwd, out float2 right)
        {
            var sin = math.sin(yaw);
            var cos = math.cos(yaw);
            fwd = new float2(-sin, cos);
            right = new float2(cos, sin);
        }

        private static float2 ComputeAcceleration(Control u, float2 fwd, float2 right, Dynamics shp, float2 vel)
        {
            var accF = ((u.thrust >= 0 ? shp.forwardAcc : shp.reverseAcc) * u.thrust) / shp.mass;

            var speedPct = math.clamp(math.length(vel) / shp.maxSpeed, 0f, 1f);
            var strafeMag = math.lerp(shp.maxStrafeAcc, shp.minStrafeAcc, speedPct);
            var accS = (strafeMag * u.strafe) / shp.mass;

            return fwd * accF + right * accS;
        }

        private static void IntegrateLinear(float2 pos, float2 vel, float2 acc, float dt, Dynamics shp,
            out float2 nextPos, out float2 nextVel)
        {
            // Match Unity Rigidbody drag: vel *= 1 / (1 + drag * dt)
            var dragFactor = 1f / (1f + shp.linearDrag * dt);
            nextVel = vel * dragFactor + acc * dt;
            var maxSpeedSq = shp.maxSpeed * shp.maxSpeed;
            if (math.lengthsq(nextVel) > maxSpeedSq)
                nextVel = math.normalize(nextVel) * shp.maxSpeed;
            nextPos = pos + nextVel * dt;
        }

        private static void IntegrateAngular(float yaw, float yawRate, float yawInput, float strafeInput,
            Config cfg, Dynamics shp, out float nextYaw, out float nextYawRate)
        {
            var torqueAlpha = shp.yawTorque * yawInput / shp.yawInertia;

            // Bank coupling: the bank spring-damper acts around the bank-tilted transform.up, so its yaw-axis projection adds a cross-coupled yaw torque the MPC must mirror to match Unity physics.
            if (shp.maxBankAngleRad > 0f)
            {
                var bankAngle = -strafeInput * shp.maxBankAngleRad;
                var sinBank = math.sin(bankAngle);
                // At steady state bankError ≈ 0, so the spring-damper reduces to rate damping; its yaw projection through the bank tilt is yawRate * sinBank.
                var bankYawTorque = -yawRate * sinBank * shp.bankDamping;
                torqueAlpha += bankYawTorque / shp.yawInertia;
            }

            // Match Unity Rigidbody drag: vel *= 1 / (1 + drag * dt)
            var dragFactor = 1f / (1f + shp.angularDrag * cfg.dt);
            nextYawRate = yawRate * dragFactor + torqueAlpha * cfg.dt;
            nextYawRate = math.clamp(nextYawRate, -shp.maxYawRate, shp.maxYawRate);
            nextYaw = Cost.WrapRadians(yaw + nextYawRate * cfg.dt);
        }
    }
}
