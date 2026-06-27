using System.Collections.Generic;
using AI.Context;
using AI.States;
using UnityEngine;

namespace AI.Utility
{
    /// <summary>
    /// Evaluates state utilities and selects the best state using either
    /// deterministic (highest utility) or probabilistic (softmax) selection.
    /// </summary>
    public class Sampler
    {
        private readonly Dictionary<AI.States.AIState, float> smoothedUtilities = new();
        private readonly UtilitySelectorSettings config;
        private readonly UtilityWeights instanceUtilityWeights;
        private readonly List<(AI.States.AIState state, float utility)> topStateUtilities = new(3);
        private readonly List<(AI.States.AIState state, float exp)> expBuffer = new(3);
        private readonly List<(AI.States.AIState state, float probability)> probabilityBuffer = new(3);

        public Dictionary<string, float> UtilityScores { get; } = new();

        public Sampler(UtilitySelectorSettings config, UtilityWeights instanceUtilityWeights)
        {
            this.config = config;
            this.instanceUtilityWeights = instanceUtilityWeights;
        }

        public AI.States.AIState Evaluate(IReadOnlyList<AI.States.AIState> states, AI.States.AIState currentAIState, float timeSinceEntry, AI.Context.AIContext context)
        {
            UtilityScores.Clear();
            if (states.Count == 0) return null;

            AI.States.AIState bestAIState = null;
            var highestUtility = float.MinValue;

            foreach (var state in states)
            {
                if (IsExcluded(state, context))
                {
                    UtilityScores[state.ProfileName] = 0f;
                    continue;
                }

                var utility = ComputeSmoothedUtility(state, currentAIState, timeSinceEntry, context);
                UtilityScores[state.ProfileName] = utility;

                if (!(utility > highestUtility)) continue;
                highestUtility = utility;
                bestAIState = state;
            }

            if (!config.useProbabilisticSampling) return bestAIState;

            topStateUtilities.Clear();
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (IsExcluded(state, context)) continue;
                AddTopState(state, UtilityScores[state.ProfileName]);
            }

            return SampleFromDistribution(topStateUtilities);
        }

        public float GetSmoothedUtility(AI.States.AIState aiState) => smoothedUtilities.GetValueOrDefault(aiState, 0f);

        private float ComputeSmoothedUtility(AI.States.AIState aiState, AI.States.AIState currentAIState, float timeSinceEntry, AIContext context)
        {
            var baseUtility = aiState.ComputeUtility(context);

            // Instance weight scales the geometric mean output before sticky bonus,
            // so the weight biases state preference without amplifying hysteresis.
            baseUtility *= ResolveWeight(aiState);

            if (aiState == currentAIState)
                baseUtility += ComputeStickyBonus(timeSinceEntry);

            return ApplySmoothing(aiState, baseUtility);
        }

        private float ComputeStickyBonus(float timeSinceEntry)
        {
            if (config.stickyFadeTime <= 0f) return config.stickyBonus;
            var fade = Mathf.Clamp01(1f - timeSinceEntry / config.stickyFadeTime);
            return config.stickyBonus * fade;
        }

        private bool IsExcluded(AI.States.AIState aiState, AIContext context = null)
        {
            if (ResolveWeight(aiState) <= 0f) return true;
            if (context != null && !aiState.IsAvailable(context)) return true;
            return false;
        }

        private float ResolveWeight(AI.States.AIState aiState)
        {
            return instanceUtilityWeights ? instanceUtilityWeights[aiState.ProfileName] : 1f;
        }

        private float ApplySmoothing(AI.States.AIState aiState, float utility)
        {
            if (config.utilitySmoothingFactor <= 0f) return utility;

            if (smoothedUtilities.TryGetValue(aiState, out var previous))
                utility = Mathf.Lerp(previous, utility, config.utilitySmoothingFactor);

            smoothedUtilities[aiState] = utility;
            return utility;
        }

        #region Probabilistic Selection

        private AI.States.AIState SampleFromDistribution(List<(AI.States.AIState state, float utility)> stateUtilities)
        {
            if (stateUtilities.Count == 0) return null;
            var probabilities = ComputeSoftmaxProbabilities(stateUtilities);
            return SampleFromProbabilities(probabilities);
        }

        public List<(AI.States.AIState state, float probability)> ComputeSoftmaxProbabilities(List<(AI.States.AIState state, float utility)> stateUtilities)
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

        private static AI.States.AIState SampleFromProbabilities(List<(AI.States.AIState state, float probability)> probabilities)
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

        private void AddTopState(AI.States.AIState aiState, float utility)
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

            topStateUtilities.Insert(insertAt, (aiState, utility));
            if (topStateUtilities.Count > 3)
                topStateUtilities.RemoveAt(3);
        }

        #endregion
    }
}
