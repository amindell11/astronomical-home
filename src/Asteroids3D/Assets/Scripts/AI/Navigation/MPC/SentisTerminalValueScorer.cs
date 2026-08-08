using System;
using Unity.Collections;
using Unity.InferenceEngine;

namespace Movement.MPC
{
    public sealed class SentisTerminalValueScorer : ITerminalValueScorer, IDisposable
    {
        private const int FeatureCount = 7;

        private readonly Worker worker;
        private NativeArray<float> inputValues;
        private Tensor<float> inputTensor;
        private Tensor outputTensor;
        private int batchSize;

        public SentisTerminalValueScorer(ModelAsset modelAsset, BackendType backendType = BackendType.CPU)
        {
            if (!modelAsset)
                throw new ArgumentNullException(nameof(modelAsset));

            worker = new Worker(ModelLoader.Load(modelAsset), backendType);
        }

        public void Score(NativeArray<State> terminalStates, NativeArray<float> values, int count)
        {
            if (count < 1 || count > terminalStates.Length || count > values.Length)
                throw new ArgumentOutOfRangeException(nameof(count), count,
                    "Count must fit both terminal-state and value buffers.");

            EnsureBatch(count);
            for (var i = 0; i < count; i++)
            {
                var state = terminalStates[i];
                var offset = i * FeatureCount;
                inputValues[offset] = state.pos.x;
                inputValues[offset + 1] = state.pos.y;
                inputValues[offset + 2] = state.vel.x;
                inputValues[offset + 3] = state.vel.y;
                inputValues[offset + 4] = state.yaw;
                inputValues[offset + 5] = state.yawRate;
                inputValues[offset + 6] = state.boostCooldownRemaining;
            }

            inputTensor.Upload(inputValues);
            worker.Schedule(inputTensor);
            worker.CopyOutput(0, ref outputTensor);
            outputTensor.CompleteAllPendingOperations();

            var output = ((Tensor<float>)outputTensor).AsReadOnlyNativeArray();
            for (var i = 0; i < count; i++)
                values[i] = output[i];
        }

        private void EnsureBatch(int count)
        {
            if (batchSize == count) return;

            inputValues.Dispose();
            inputTensor?.Dispose();
            outputTensor?.Dispose();

            batchSize = count;
            inputValues = new NativeArray<float>(count * FeatureCount, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            inputTensor = new Tensor<float>(new TensorShape(count, FeatureCount));
            outputTensor = new Tensor<float>(new TensorShape(count, 1));
        }

        public void Dispose()
        {
            worker.Dispose();
            inputValues.Dispose();
            inputTensor?.Dispose();
            outputTensor?.Dispose();
        }
    }
}
