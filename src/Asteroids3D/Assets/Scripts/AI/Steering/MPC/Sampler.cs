using AI.Scanning;
using UnityEngine;

namespace Movement.MPC
{
    /// <summary>
    /// Random sampling solver for MPC.
    /// </summary>
    public static partial class Sampler
    {
        static partial void BeginSolveProfiling();
        static partial void EndSolveProfiling();
        static partial void BeginGenerateCandidateProfiling();
        static partial void EndGenerateCandidateProfiling();
        static partial void BeginEvaluateTrajectoryProfiling();
        static partial void EndEvaluateTrajectoryProfiling();

        public static float Solve(State initialState, Control[] warmStart, Control[] candidateBuffer, Vector2 goalPos,
            ObstacleScan scan, Config cfg, Dynamics shp,
            int samples, float noiseStd, Control[] resultBuffer, Control lastControl)
        {
            BeginSolveProfiling();
            try
            {
            var horizon = cfg.horizon;
            var bestCost = EvaluateTrajectory(initialState, warmStart, goalPos, scan, cfg, shp, lastControl);
            System.Array.Copy(warmStart, resultBuffer, horizon);
            
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
            finally
            {
                EndSolveProfiling();
            }
        }

        private static void GenerateCandidate(Control[] warmStart, Control[] candidate, int horizon, float noiseStd)
        {
            BeginGenerateCandidateProfiling();
            try
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
            finally
            {
                EndGenerateCandidateProfiling();
            }
        }

        private static float EvaluateTrajectory(State state, Control[] sequence, Vector2 goalPos,
            ObstacleScan scan, Config cfg, Dynamics shp, Control lastControl)
        {
            BeginEvaluateTrajectoryProfiling();
            try
            {
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
            finally
            {
                EndEvaluateTrajectoryProfiling();
            }
        }


        private static float RandomGaussian()
        {
            var u1 = 1.0f - Random.value;
            var u2 = 1.0f - Random.value;
            return Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        }
    }
}
