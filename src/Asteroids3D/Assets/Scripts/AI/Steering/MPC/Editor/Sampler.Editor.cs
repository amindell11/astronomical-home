#if UNITY_EDITOR
using UnityEngine;

namespace AI.Steering.MPC
{
    public static partial class Sampler
    {
        public static CostBreakdown EvaluateTrajectoryBreakdown(State state, Control[] sequence, Vector2 goalPos,
            Scanning.ObstacleScan scan, Config cfg, Dynamics shp, Control lastControl)
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
