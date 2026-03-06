#if UNITY_EDITOR
using AI.Scanning;
using UnityEngine;
using UnityEngine.Profiling;

namespace Movement.MPC
{
    public static partial class Sampler
    {
        static partial void BeginSolveProfiling() => Profiler.BeginSample("MPC.Sampler.Solve");

        static partial void EndSolveProfiling() => Profiler.EndSample();

        static partial void BeginGenerateCandidateProfiling() => Profiler.BeginSample("MPC.Sampler.GenerateCandidate");

        static partial void EndGenerateCandidateProfiling() => Profiler.EndSample();

        static partial void BeginEvaluateTrajectoryProfiling() => Profiler.BeginSample("MPC.Sampler.EvaluateTrajectory");

        static partial void EndEvaluateTrajectoryProfiling() => Profiler.EndSample();

        public static CostBreakdown EvaluateTrajectoryBreakdown(State state, Control[] sequence, Vector2 goalPos,
            ObstacleScan scan, Config cfg, Dynamics shp, Control lastControl)
        {
            var totalBreakdown = new CostBreakdown();
            var current = state;
            var prevU = lastControl;

            for (var i = 0; i < cfg.horizon; i++)
            {
                var u = sequence[i];
                var isTerminal = i == cfg.horizon - 1;
                totalBreakdown.Add(Cost.EvaluateBreakdown(current, u, prevU, goalPos, scan, cfg, isTerminal));
                current = Model.Step(current, u, cfg, shp);
                prevU = u;
            }

            return totalBreakdown;
        }
    }
}
#endif
