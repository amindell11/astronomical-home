using Movement;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace AI.Navigation.MPC
{
    [BurstCompile(CompileSynchronously = true)]
    public struct EvaluateCandidatesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Control> candidates;
        public NativeArray<float> costs;

        [ReadOnly] public CostInput costInput;
        public State initialState;
        public Config cfg;
        public Dynamics dynamics;
        public Control lastControl;

        public void Execute(int candidateIndex)
        {
            var horizon = cfg.horizon;
            var offset = candidateIndex * horizon;

            var totalCost = 0f;
            var current = initialState;
            var prevU = lastControl;

            for (var i = 0; i < horizon; i++)
            {
                var u = candidates[offset + i];
                totalCost += Cost.Evaluate(current, u, prevU, costInput, cfg, i);
                current = Model.Step(current, u, cfg, dynamics);
                prevU = u;
            }

            costs[candidateIndex] = totalCost;
        }
    }
}
