using UnityEngine;
using System.Collections.Generic;

namespace AI.Steering
{
    public struct MpcState
    {
        public Vector2 pos;
        public Vector2 vel;
        public float yaw; // Radians
        public float yawRate; // Radians per second
    }

    public struct MpcControl
    {
        public float thrust;
        public float strafe;
        public float yawTorque;
    }

    public class MpcController
    {
        public struct Config
        {
            public float dt;
            public int horizon;
            public float maxSpeed;
            public float maxYawRate;
            public float forwardAcc;
            public float reverseAcc;
            public float strafeAcc;
            public float alphaMax;
            public float damping;

            // Weights
            public float wPos;
            public float wVel;
            public float wYaw;
            public float wYawRate;
            public float wEffort;
            public float wSmoothness;
            public float terminalMultiplier;
        }

        public static float EvaluateStepCost(MpcState s, MpcControl u, MpcControl prevU, Vector2 goalPos, Config cfg, bool isTerminal)
        {
            // 1. Position cost
            var posCost = (s.pos - goalPos).sqrMagnitude;

            // 2. Velocity cost (damping towards zero/goal vel)
            var velCost = s.vel.sqrMagnitude;

            // 3. Heading cost (face the goal)
            var toGoal = (goalPos - s.pos);
            float headingCost = 0;
            if (toGoal.sqrMagnitude > 0.01f)
            {
                var dirToGoal = toGoal.normalized;
                // Forward vector in body space
                var fwd = new Vector2(-Mathf.Sin(s.yaw), Mathf.Cos(s.yaw));
                headingCost = 1f - Vector2.Dot(fwd, dirToGoal);
            }

            // 4. Yaw rate cost
            var yawRateCost = s.yawRate * s.yawRate;

            // 5. Effort cost
            var effortCost = (u.thrust * u.thrust) + (u.strafe * u.strafe) + (u.yawTorque * u.yawTorque);

            // 6. Smoothness (delta-u)
            var duT = u.thrust - prevU.thrust;
            var duS = u.strafe - prevU.strafe;
            var duY = u.yawTorque - prevU.yawTorque;
            var smoothnessCost = (duT * duT) + (duS * duS) + (duY * duY);

            var total = (posCost * cfg.wPos) +
                        (velCost * cfg.wVel) +
                        (headingCost * cfg.wYaw) +
                        (yawRateCost * cfg.wYawRate) +
                        (effortCost * cfg.wEffort) +
                        (smoothnessCost * cfg.wSmoothness);

            if (isTerminal) total *= cfg.terminalMultiplier;

            return total;
        }

        public static float Solve(MpcState initialState, MpcControl[] warmStart, Vector2 goalPos, Config cfg, int samples, float noiseStd, MpcControl[] resultBuffer)
        {
            var horizon = cfg.horizon;
            var bestCost = EvaluateTrajectory(initialState, warmStart, goalPos, cfg);
            System.Array.Copy(warmStart, resultBuffer, horizon);

            var candidate = new MpcControl[horizon];

            for (var i = 0; i < samples - 1; i++)
            {
                for (var j = 0; j < horizon; j++)
                {
                    candidate[j] = new MpcControl
                    {
                        thrust = Mathf.Clamp(warmStart[j].thrust + RandomGaussian() * noiseStd, -1f, 1f),
                        strafe = Mathf.Clamp(warmStart[j].strafe + RandomGaussian() * noiseStd, -1f, 1f),
                        yawTorque = Mathf.Clamp(warmStart[j].yawTorque + RandomGaussian() * noiseStd, -1f, 1f)
                    };
                }

                var cost = EvaluateTrajectory(initialState, candidate, goalPos, cfg);
                if (cost >= bestCost) continue;
                bestCost = cost;
                System.Array.Copy(candidate, resultBuffer, horizon);
            }

            return bestCost;
        }

        private static float EvaluateTrajectory(MpcState state, MpcControl[] sequence, Vector2 goalPos, Config cfg)
        {
            var totalCost = 0f;
            var current = state;
            var prevU = new MpcControl();

            for (var i = 0; i < cfg.horizon; i++)
            {
                var u = sequence[i];
                var isTerminal = (i == cfg.horizon - 1);
                totalCost += EvaluateStepCost(current, u, prevU, goalPos, cfg, isTerminal);
                current = Step(current, u, cfg);
                prevU = u;
            }

            return totalCost;
        }

        private static float RandomGaussian()
        {
            var u1 = 1.0f - Random.value;
            var u2 = 1.0f - Random.value;
            return Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        }
        
        public static MpcState Step(MpcState s, MpcControl u, Config cfg)
        {
            // Body axes from yaw (0 deg is +Y in this project)
            // Kinematics.Forward uses: new Vector2(-Mathf.Sin(Yaw * Mathf.Deg2Rad), Mathf.Cos(Yaw * Mathf.Deg2Rad))
            var sin = Mathf.Sin(s.yaw);
            var cos = Mathf.Cos(s.yaw);
            var fwd = new Vector2(-sin, cos);
            var right = new Vector2(cos, sin);

            // Linear acceleration
            var accF = (u.thrust >= 0 ? cfg.forwardAcc : cfg.reverseAcc) * u.thrust;
            var accS = cfg.strafeAcc * u.strafe;
            var acc = fwd * accF + right * accS;

            // Integrate velocity and position
            var nextVel = s.vel + acc * cfg.dt;
            if (nextVel.sqrMagnitude > cfg.maxSpeed * cfg.maxSpeed)
            {
                nextVel = nextVel.normalized * cfg.maxSpeed;
            }
            var nextPos = s.pos + nextVel * cfg.dt;

            // Yaw dynamics (simplified 2nd order)
            var alpha = cfg.alphaMax * u.yawTorque - cfg.damping * s.yawRate;
            var nextYawRate = Mathf.Clamp(s.yawRate + alpha * cfg.dt, -cfg.maxYawRate, cfg.maxYawRate);
            var nextYaw = s.yaw + nextYawRate * cfg.dt;
            
            // Wrap yaw to [-PI, PI]
            while (nextYaw > Mathf.PI) nextYaw -= 2f * Mathf.PI;
            while (nextYaw < -Mathf.PI) nextYaw += 2f * Mathf.PI;

            return new MpcState
            {
                pos = nextPos,
                vel = nextVel,
                yaw = nextYaw,
                yawRate = nextYawRate
            };
        }
    }
}
