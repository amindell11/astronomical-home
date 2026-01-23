using UnityEngine;

namespace AI.Steering.MPC
{
    /// <summary>
    /// Random sampling solver for MPC.
    /// </summary>
    public static class Sampler
    {
        public static float Solve(State initialState, Control[] warmStart, Vector2 goalPos,
            Scanning.DetectedObstacle[] obstacles, int obstacleCount, Config cfg, Dynamics shp,
            int samples, float noiseStd, Control[] resultBuffer)
        {
            var horizon = cfg.horizon;
            var bestCost = EvaluateTrajectory(initialState, warmStart, goalPos, obstacles, obstacleCount, cfg, shp);
            System.Array.Copy(warmStart, resultBuffer, horizon);

            var candidate = new Control[horizon];

            for (var i = 0; i < samples - 1; i++)
            {
                GenerateCandidate(warmStart, candidate, horizon, noiseStd);
                var cost = EvaluateTrajectory(initialState, candidate, goalPos, obstacles, obstacleCount, cfg, shp);
                
                if (cost >= bestCost) continue;
                bestCost = cost;
                System.Array.Copy(candidate, resultBuffer, horizon);
            }

            return bestCost;
        }

        private static void GenerateCandidate(Control[] warmStart, Control[] candidate, int horizon, float noiseStd)
        {
            for (var j = 0; j < horizon; j++)
            {
                candidate[j] = new Control
                {
                    thrust = Mathf.Clamp(warmStart[j].thrust + RandomGaussian() * noiseStd, -1f, 1f),
                    strafe = Mathf.Clamp(warmStart[j].strafe + RandomGaussian() * noiseStd, -1f, 1f),
                    yawTorque = Mathf.Clamp(warmStart[j].yawTorque + RandomGaussian() * noiseStd, -1f, 1f)
                };
            }
        }

        private static float EvaluateTrajectory(State state, Control[] sequence, Vector2 goalPos,
            Scanning.DetectedObstacle[] obstacles, int obstacleCount, Config cfg, Dynamics shp)
        {
            var totalCost = 0f;
            var current = state;
            var prevU = new Control();

            for (var i = 0; i < cfg.horizon; i++)
            {
                var u = sequence[i];
                var isTerminal = i == cfg.horizon - 1;
                totalCost += Cost.Evaluate(current, u, prevU, goalPos, obstacles, obstacleCount, cfg, isTerminal);
                current = Model.Step(current, u, cfg, shp);
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
    }
}
