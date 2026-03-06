using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Movement.MPC
{
    [BurstCompile]
    public struct EvaluateCandidatesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Control> warmStart;
        [NativeDisableParallelForRestriction]
        public NativeArray<Control> candidates;
        public NativeArray<float> costs;

        [ReadOnly] public CostInput costInput;
        public State initialState;
        public Config cfg;
        public Dynamics dynamics;
        public Control lastControl;
        public float noiseStd;
        public uint rngSeed;

        public void Execute(int candidateIndex)
        {
            var horizon = cfg.horizon;
            var offset = candidateIndex * horizon;

            if (candidateIndex == 0)
            {
                for (var j = 0; j < horizon; j++)
                    candidates[offset + j] = warmStart[j];
            }
            else
            {
                var rng = new Unity.Mathematics.Random(rngSeed + (uint)candidateIndex);
                for (var j = 0; j < horizon; j++)
                {
                    var warm = warmStart[j];
                    candidates[offset + j] = new Control
                    {
                        thrust = math.clamp(warm.thrust + NextGaussian(ref rng) * noiseStd, -1f, 1f),
                        strafe = math.clamp(warm.strafe + NextGaussian(ref rng) * noiseStd, -1f, 1f),
                        yawTorque = math.clamp(warm.yawTorque + NextGaussian(ref rng) * noiseStd, -1f, 1f)
                    };
                }
            }

            var totalCost = 0f;
            var current = initialState;
            var prevU = lastControl;

            for (var i = 0; i < horizon; i++)
            {
                var u = candidates[offset + i];
                var isTerminal = i == horizon - 1;
                totalCost += Cost.Evaluate(current, u, prevU, costInput, cfg, isTerminal);
                current = Model.Step(current, u, cfg, dynamics);
                prevU = u;
            }

            costs[candidateIndex] = totalCost;
        }

        private static float NextGaussian(ref Unity.Mathematics.Random rng)
        {
            var u1 = 1f - rng.NextFloat();
            var u2 = 1f - rng.NextFloat();
            return math.sqrt(-2f * math.log(u1)) * math.sin(2f * math.PI * u2);
        }
    }

    public static class BurstSampler
    {
        public static float Solve(
            State initialState,
            NativeArray<Control> warmStart,
            NativeArray<Control> candidates,
            NativeArray<float> costs,
            CostInput costInput,
            Config cfg,
            Dynamics dynamics,
            int samples,
            float noiseStd,
            Control lastControl,
            NativeArray<Control> resultBuffer)
        {
            var horizon = cfg.horizon;
            var rngSeed = (uint)(Time.frameCount * 7919 + initialState.pos.GetHashCode());
            if (rngSeed == 0) rngSeed = 1;

            var job = new EvaluateCandidatesJob
            {
                warmStart = warmStart,
                candidates = candidates,
                costs = costs,
                costInput = costInput,
                initialState = initialState,
                cfg = cfg,
                dynamics = dynamics,
                lastControl = lastControl,
                noiseStd = noiseStd,
                rngSeed = rngSeed
            };

            job.Schedule(samples, 1).Complete();

            var bestCost = costs[0];
            var bestIndex = 0;
            for (var i = 1; i < samples; i++)
            {
                if (costs[i] < bestCost)
                {
                    bestCost = costs[i];
                    bestIndex = i;
                }
            }

            NativeArray<Control>.Copy(candidates, bestIndex * horizon, resultBuffer, 0, horizon);
            return bestCost;
        }
    }
}
