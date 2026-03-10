using System.Collections.Generic;
using AI.States;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.Utility
{
    /// <summary>
    /// Evaluates state utilities and selects the best state using either
    /// deterministic (highest utility) or probabilistic (softmax) selection.
    /// </summary>
    public class Sampler
    {
        private readonly Dictionary<AI.States.State, float> smoothedUtilities = new();
        private readonly UtilitySelectorSettings config;
        private readonly UtilityWeights instanceUtilityWeights;
        private readonly List<(AI.States.State state, float utility)> topStateUtilities = new(3);
        private readonly List<(AI.States.State state, float exp)> expBuffer = new(3);
        private readonly List<(AI.States.State state, float probability)> probabilityBuffer = new(3);
        private UtilityTuning utilityTuning;

        public Dictionary<StateType, float> UtilityScores { get; } = new();

        public Sampler(UtilitySelectorSettings config, UtilityWeights instanceUtilityWeights)
        {
            this.config = config;
            this.instanceUtilityWeights = instanceUtilityWeights;
        }

        public void SetTuning(UtilityTuning tun) => utilityTuning = tun;

        public AI.States.State Evaluate(IReadOnlyList<AI.States.State> states, AI.States.State currentState, float timeSinceEntry, AI.Context.Info context)
        {
            UtilityScores.Clear();
            if (states.Count == 0) return null;

            AI.States.State bestState = null;
            var highestUtility = float.MinValue;

            foreach (var state in states)
            {
                var utility = ComputeSmoothedUtility(state, currentState, timeSinceEntry, context);
                UtilityScores[state.Type] = utility;

                if (!(utility > highestUtility)) continue;
                highestUtility = utility;
                bestState = state;
            }

            if (!config.useProbabilisticSampling) return bestState;

            topStateUtilities.Clear();
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                AddTopState(state, UtilityScores[state.Type]);
            }

            return SampleFromDistribution(topStateUtilities);
        }

        public float GetSmoothedUtility(AI.States.State state) => smoothedUtilities.GetValueOrDefault(state, 0f);

        private float ComputeSmoothedUtility(AI.States.State state, AI.States.State currentState, float timeSinceEntry, Info context)
        {
            var baseUtility = state.ComputeUtility(context);

            if (state == currentState)
                baseUtility += ComputeStickyBonus(timeSinceEntry);

            var weighted = ApplyWeight(state, baseUtility);
            return ApplySmoothing(state, weighted);
        }

        private float ComputeStickyBonus(float timeSinceEntry)
        {
            if (config.stickyFadeTime <= 0f) return config.stickyBonus;
            var fade = Mathf.Clamp01(1f - timeSinceEntry / config.stickyFadeTime);
            return config.stickyBonus * fade;
        }

        private float ApplyWeight(AI.States.State state, float baseUtility)
        {
            if (state == null) return baseUtility;

            var baseWeight = utilityTuning && utilityTuning.utilityWeights ? utilityTuning.utilityWeights[state.Type] : 1f;
            var instanceBias = instanceUtilityWeights ? instanceUtilityWeights[state.Type] : 1f;
            return baseUtility * baseWeight * instanceBias;
        }

        private float ApplySmoothing(AI.States.State state, float utility)
        {
            if (config.utilitySmoothingFactor <= 0f) return utility;

            if (smoothedUtilities.TryGetValue(state, out var previous))
                utility = Mathf.Lerp(previous, utility, config.utilitySmoothingFactor);

            smoothedUtilities[state] = utility;
            return utility;
        }

        #region Probabilistic Selection

        private AI.States.State SampleFromDistribution(List<(AI.States.State state, float utility)> stateUtilities)
        {
            if (stateUtilities.Count == 0) return null;
            var probabilities = ComputeSoftmaxProbabilities(stateUtilities);
            return SampleFromProbabilities(probabilities);
        }

        public List<(AI.States.State state, float probability)> ComputeSoftmaxProbabilities(List<(AI.States.State state, float utility)> stateUtilities)
        {
            expBuffer.Clear();
            probabilityBuffer.Clear();

            var invTemperature = 1f / config.samplingTemperature;
            var maxUtility = float.NegativeInfinity;
            for (var i = 0; i < stateUtilities.Count; i++)
            {
                var scaledUtility = stateUtilities[i].utility * invTemperature;
                if (scaledUtility > maxUtility)
                    maxUtility = scaledUtility;
            }

            var expSum = 0f;
            for (var i = 0; i < stateUtilities.Count; i++)
            {
                var entry = stateUtilities[i];
                var exp = Mathf.Exp(entry.utility * invTemperature - maxUtility);
                expBuffer.Add((entry.state, exp));
                expSum += exp;
            }

            var fallbackProbability = expBuffer.Count > 0 ? 1f / expBuffer.Count : 0f;
            for (var i = 0; i < expBuffer.Count; i++)
            {
                var entry = expBuffer[i];
                var probability = expSum > 0f ? entry.exp / expSum : fallbackProbability;
                probabilityBuffer.Add((entry.state, probability));
            }

            return probabilityBuffer;
        }

        private static AI.States.State SampleFromProbabilities(List<(AI.States.State state, float probability)> probabilities)
        {
            if (probabilities.Count == 0) return null;

            var random = Random.Range(0f, 1f);
            var cumulative = 0f;

            foreach (var (state, probability) in probabilities)
            {
                cumulative += probability;
                if (random <= cumulative) return state;
            }

            return probabilities[^1].state;
        }

        private void AddTopState(AI.States.State state, float utility)
        {
            var insertAt = topStateUtilities.Count;
            for (var i = 0; i < topStateUtilities.Count; i++)
            {
                if (!(utility > topStateUtilities[i].utility))
                    continue;

                insertAt = i;
                break;
            }

            if (insertAt >= 3)
                return;

            topStateUtilities.Insert(insertAt, (state, utility));
            if (topStateUtilities.Count > 3)
                topStateUtilities.RemoveAt(3);
        }

        #endregion
    }
}
