using System;
using System.Collections.Generic;
using System.Linq;
using AI.Context;
using AI.States;
using UnityEngine;

namespace AI.Utility
{
    /// <summary>
    /// Orchestrates AI state lifecycle and transitions.
    /// Delegates utility evaluation and selection to UtilitySampler.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public partial class UtilitySelector : MonoBehaviour
    {
        [SerializeField] private UtilitySelectorSettings config;
        
        [Header("Instance Weights")]
        [Tooltip("Per-instance weight biases. Swap to create different AI personalities.")]
        [SerializeField] private UtilityWeights instanceUtilityWeights;

        private readonly List<AI.States.AIState> states = new();
        private Sampler sampler;
        private float stateChangeTime;

        public AI.States.AIState CurrentAIState { get; private set; }
        public AIContext Context { get; private set; }
        public string CurrentStateName => CurrentAIState?.ProfileName ?? "None";
        public Dictionary<string, float> UtilityScores => sampler?.UtilityScores;
        public IReadOnlyList<AI.States.AIState> RegisteredStates => states;
        public UtilitySelectorSettings Config => config;

        /// <summary>Fired on state transitions: (fromState, toState). Null fromState on first entry.</summary>
        public event Action<AI.States.AIState, AI.States.AIState> OnStateTransition;

        private void Awake()
        {
            stateChangeTime = Time.time;
            sampler = new Sampler(config, instanceUtilityWeights);
        }

        public void Initialize(params AI.States.AIState[] statesToAdd)
        {
            states.Clear();
            states.AddRange(statesToAdd.Where(s => s != null));
            if (states.Count == 0) return;

            if (CurrentAIState != null) return;
            TransitionTo(states[0], null);
        }
        
        public void Tick(AIContext context, float deltaTime)
        {
            Context = context;
            if (states.Count == 0) return;

            CurrentAIState?.Tick(context, deltaTime);

            var timeSinceChange = Time.time - stateChangeTime;
            var selectedState = sampler.Evaluate(states, CurrentAIState, timeSinceChange, context);

            if (ShouldTransition(selectedState, timeSinceChange))
                TransitionTo(selectedState, context);
        }

        private bool ShouldTransition(AI.States.AIState selectedAIState, float timeSinceChange)
        {
            if (selectedAIState == null || selectedAIState == CurrentAIState) return false;
            if (timeSinceChange < config.minTimeInState) return false;
            if (config.useProbabilisticSampling) return true;

            var selectedUtility = sampler.GetSmoothedUtility(selectedAIState);
            var currentUtility = sampler.GetSmoothedUtility(CurrentAIState);
            return (selectedUtility - currentUtility) > config.utilityThreshold;
        }

        public void ForceTransition(AI.States.AIState newAIState, AIContext context)
        {
            if (newAIState != null && states.Contains(newAIState))
                TransitionTo(newAIState, context);
        }

        private void TransitionTo(AI.States.AIState newAIState, AIContext context)
        {
            if (newAIState == CurrentAIState) return;
            var prev = CurrentAIState;
            CurrentAIState?.Exit();
            CurrentAIState = newAIState;
            CurrentAIState.Enter(context);
            stateChangeTime = Time.time;
            OnStateTransition?.Invoke(prev, newAIState);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Resets selector state so Initialize() can register a new state set.
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
