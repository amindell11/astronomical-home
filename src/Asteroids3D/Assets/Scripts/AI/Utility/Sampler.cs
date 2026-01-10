using System.Collections.Generic;
using System.Linq;
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
            
            var topStates = states.OrderByDescending(s => UtilityScores[s.Type]).Take(3);
            return SampleFromDistribution(topStates.Select(s => (state: s, utility: UtilityScores[s.Type])).ToList());
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
            var maxUtility = stateUtilities.Max(s => s.utility / config.samplingTemperature);

            var expValues = stateUtilities
                .Select(s => (s.state, exp: Mathf.Exp(s.utility / config.samplingTemperature - maxUtility)))
                .ToList();

            var expSum = expValues.Sum(e => e.exp);

            return expValues
                .Select(e => (e.state, probability: expSum > 0f ? e.exp / expSum : 1f / expValues.Count))
                .ToList();
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

        #endregion
    }
}
