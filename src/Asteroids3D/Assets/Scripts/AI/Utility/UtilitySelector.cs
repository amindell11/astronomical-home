using System.Collections.Generic;
using System.Linq;
using AI.States;
using UnityEngine;
using Info = AI.Context.Info;

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

        private readonly List<AI.States.State> states = new();
        private Sampler sampler;
        private float stateChangeTime;

        public AI.States.State CurrentState { get; private set; }
        public Info Context { get; private set; }
        public string CurrentStateName => CurrentState?.Type.ToString() ?? "None";
        public Dictionary<StateType, float> UtilityScores => sampler?.UtilityScores;
        public UtilitySelectorSettings Config => config;

        private void Awake()
        {
            stateChangeTime = Time.time;
            sampler = new Sampler(config, instanceUtilityWeights);
        }

        public void Initialize(params AI.States.State[] statesToAdd)
        {
            states.Clear(); 
            states.AddRange(statesToAdd.Where(s => s != null));
            if (states.Count == 0) return;
            
            sampler.SetTuning(states[0].GetTuning());
            
            if (CurrentState != null) return;
            TransitionTo(states[0], null);
        }
        
        public void Tick(Info context, float deltaTime)
        {
            Context = context;
            if (states.Count == 0) return;

            CurrentState?.Tick(context, deltaTime);

            var timeSinceChange = Time.time - stateChangeTime;
            var selectedState = sampler.Evaluate(states, CurrentState, timeSinceChange, context);

            if (ShouldTransition(selectedState, timeSinceChange))
                TransitionTo(selectedState, context);
        }

        private bool ShouldTransition(AI.States.State selectedState, float timeSinceChange)
        {
            if (selectedState == null || selectedState == CurrentState) return false;
            if (timeSinceChange < config.minTimeInState) return false;
            if (config.useProbabilisticSampling) return true;

            var selectedUtility = sampler.GetSmoothedUtility(selectedState);
            var currentUtility = sampler.GetSmoothedUtility(CurrentState);
            return (selectedUtility - currentUtility) > config.utilityThreshold;
        }

        public void ForceTransition(AI.States.State newState, Info context)
        {
            if (newState != null && states.Contains(newState))
                TransitionTo(newState, context);
        }

        private void TransitionTo(AI.States.State newState, Info context)
        {
            if (newState == CurrentState) return;
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter(context);
            stateChangeTime = Time.time;
        }
    }
}
