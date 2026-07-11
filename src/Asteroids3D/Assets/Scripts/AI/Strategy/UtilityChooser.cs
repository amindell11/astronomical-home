using System;
using System.Collections.Generic;
using AI.Context;
using AI.States;
using Ships.Command;
using UnityEngine;

namespace AI.Utility
{
    /// <summary>
    /// Utility-based decision policy: orchestrates AI state lifecycle and transitions,
    /// delegating utility evaluation and selection to <see cref="Sampler"/>. One
    /// implementation of <see cref="IStateChooser"/>, hosted by <see cref="Brain"/>.
    /// </summary>
    [Serializable]
    public class UtilityChooser : IStateChooser
    {
        [SerializeField] private UtilitySelectorSettings config;

        [Header("Instance Weights")]
        [Tooltip("Per-instance weight biases. Swap to create different AI personalities.")]
        [SerializeField] private UtilityWeights instanceUtilityWeights;

        private readonly List<AIState> states = new();
        private Sampler sampler;
        private float simTime;
        private float stateChangeTime;

        public AIState CurrentAIState { get; private set; }
        public AIContext Context { get; private set; }
        public string CurrentStateName => CurrentAIState?.ProfileName ?? "None";
        public Dictionary<string, float> UtilityScores => sampler?.UtilityScores;
        public IReadOnlyList<AIState> RegisteredStates => states;
        public UtilitySelectorSettings Config => config;
        internal Sampler Sampler => sampler;

        /// <summary>Fired on state transitions: (fromState, toState). Null fromState on first entry.</summary>
        public event Action<AIState, AIState> OnStateTransition;

        public void Initialize(IReadOnlyList<AIState> statesToAdd, SeedScope samplerScope)
        {
            sampler ??= new Sampler(config, instanceUtilityWeights, samplerScope.ToSeed());
            stateChangeTime = simTime;

            states.Clear();
            if (statesToAdd != null)
                foreach (var s in statesToAdd)
                    if (s != null) states.Add(s);
            if (states.Count == 0) return;

            if (CurrentAIState != null) return;
            TransitionTo(states[0], null);
        }

        public NavigationIntent Decide(AIContext context, float deltaTime)
        {
            Context = context;
            if (states.Count == 0) return NavigationIntent.None;

            simTime += deltaTime;
            var intent = CurrentAIState?.Tick(context, deltaTime) ?? NavigationIntent.None;

            var timeSinceChange = simTime - stateChangeTime;
            var selectedState = sampler.Evaluate(states, CurrentAIState, timeSinceChange, context);

            if (!ShouldTransition(selectedState, timeSinceChange))
                return intent;

            // Transitioning: the new state ticks next frame, so reset the actuators this
            // frame (matching the old Exit→ResetNavigation behavior) by returning None.
            TransitionTo(selectedState, context);
            return NavigationIntent.None;
        }

        private bool ShouldTransition(AIState selectedAIState, float timeSinceChange)
        {
            if (selectedAIState == null || selectedAIState == CurrentAIState) return false;
            if (timeSinceChange < config.minTimeInState) return false;
            if (config.useProbabilisticSampling) return true;

            var selectedUtility = sampler.GetSmoothedUtility(selectedAIState);
            var currentUtility = sampler.GetSmoothedUtility(CurrentAIState);
            return (selectedUtility - currentUtility) > config.utilityThreshold;
        }

        public void ForceTransition(AIState newAIState, AIContext context)
        {
            if (newAIState != null && states.Contains(newAIState))
                TransitionTo(newAIState, context);
        }

        private void TransitionTo(AIState newAIState, AIContext context)
        {
            if (newAIState == CurrentAIState) return;
            var prev = CurrentAIState;
            CurrentAIState?.Exit();
            CurrentAIState = newAIState;
            CurrentAIState.Enter(context);
            stateChangeTime = simTime;
            OnStateTransition?.Invoke(prev, newAIState);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Resets chooser state so Initialize() can register a new state set.
        /// Editor/test-only — zero production impact.
        /// </summary>
        public void ResetForTesting()
        {
            CurrentAIState?.Exit();
            CurrentAIState = null;
            states.Clear();
        }
#endif
    }
}
