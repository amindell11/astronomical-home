using AI.Scanning;
using UnityEngine;

namespace Movement.MPC
{
    /// <summary>
    /// Random sampling solver for MPC.
    /// </summary>
    public static partial class Sampler
    {
        public static float Solve(State initialState, Control[] warmStart, Control[] candidateBuffer, Vector2 goalPos,
            ObstacleScan scan, Config cfg, Dynamics shp,
            int samples, float noiseStd, Control[] resultBuffer, Control lastControl)
        {
            using var _ = EditorProfilingScope.Begin("MPC.Sampler.Solve");
            var horizon = cfg.horizon;
            var bestCost = EvaluateTrajectory(initialState, warmStart, goalPos, scan, cfg, shp, lastControl);
            if (!ReferenceEquals(warmStart, resultBuffer))
            {
                System.Array.Copy(warmStart, resultBuffer, horizon);
            }
            
            if (candidateBuffer == null || candidateBuffer.Length < horizon)
            {
                candidateBuffer = new Control[horizon];
            }

            for (var i = 0; i < samples - 1; i++)
            {
                GenerateCandidate(warmStart, candidateBuffer, horizon, noiseStd);
                var cost = EvaluateTrajectory(initialState, candidateBuffer, goalPos, scan, cfg, shp, lastControl);
                
                if (cost >= bestCost) continue;
                bestCost = cost;
                System.Array.Copy(candidateBuffer, resultBuffer, horizon);
            }

            return bestCost;
        }

        private static void GenerateCandidate(Control[] warmStart, Control[] candidate, int horizon, float noiseStd)
        {
            using var _ = EditorProfilingScope.Begin("MPC.Sampler.GenerateCandidate");
            for (var j = 0; j < horizon; j++)
            {
                var warm = warmStart[j];
                candidate[j] = new Control
                {
                    thrust = Mathf.Clamp(warm.thrust + RandomGaussian() * noiseStd, -1f, 1f),
                    strafe = Mathf.Clamp(warm.strafe + RandomGaussian() * noiseStd, -1f, 1f),
                    yawTorque = Mathf.Clamp(warm.yawTorque + RandomGaussian() * noiseStd, -1f, 1f)
                };
            }
        }

        private static float EvaluateTrajectory(State state, Control[] sequence, Vector2 goalPos,
            ObstacleScan scan, Config cfg, Dynamics shp, Control lastControl)
        {
            using var _ = EditorProfilingScope.Begin("MPC.Sampler.EvaluateTrajectory");
            var totalCost = 0f;
            var current = state;
            var prevU = lastControl;

            for (var i = 0; i < cfg.horizon; i++)
            {
                var u = sequence[i];
                var isTerminal = i == cfg.horizon - 1;
                totalCost += Cost.Evaluate(current, u, prevU, goalPos, scan, cfg, isTerminal);
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
