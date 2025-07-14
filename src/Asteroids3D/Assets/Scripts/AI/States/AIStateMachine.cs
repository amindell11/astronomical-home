using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// State machine that manages AI states based on utility scores.
    /// Handles state transitions with hysteresis to prevent thrashing.
    /// </summary>
    public class AIStateMachine
    {
        private readonly List<AIState> states = new List<AIState>();
        private AIState currentState;
        private float stateChangeTime;
        
        // Hysteresis parameters
        private readonly float minTimeInState;
        private readonly float utilityThreshold;
        
        public AIState CurrentState => currentState;
        public string CurrentStateName => currentState?.GetType().Name ?? "None";
        
        /// <summary>
        /// Dictionary of utility scores for debugging
        /// </summary>
        public Dictionary<string, float> UtilityScores { get; private set; } = new Dictionary<string, float>();

        public AIStateMachine(float minTimeInState = 0.5f, float utilityThreshold = 0.1f)
        {
            this.minTimeInState = minTimeInState;
            this.utilityThreshold = utilityThreshold;
            stateChangeTime = Time.time;
        }

        /// <summary>
        /// Add a state to the machine
        /// </summary>
        public void AddState(AIState state)
        {
            if (state != null && !states.Contains(state))
            {
                states.Add(state);
            }
        }

        /// <summary>
        /// Initialize the state machine with a collection of states
        /// </summary>
        public void Initialize(params AIState[] statesToAdd)
        {
            states.Clear();
            foreach (var state in statesToAdd)
            {
                AddState(state);
            }
            
            // Start with the first state if available
            if (states.Count > 0 && currentState == null)
            {
                currentState = states[0];
                currentState.Enter(null);
                stateChangeTime = Time.time;
            }
        }

        /// <summary>
        /// Update the state machine, evaluating utilities and transitioning if needed
        /// </summary>
        public void Update(AIContext context, float deltaTime)
        {
            if (states.Count == 0) return;
            
            // Update current state
            currentState?.Tick(context, deltaTime);
            
            // Evaluate utilities for all states
            UtilityScores.Clear();
            AIState bestState = null;
            float highestUtility = -1f;
            
            foreach (var state in states)
            {
                float utility = state.ComputeUtility(context);
                UtilityScores[state.GetType().Name] = utility;
                
                if (utility > highestUtility)
                {
                    highestUtility = utility;
                    bestState = state;
                }
            }
            
            // Check if we should transition
            if (bestState != null && bestState != currentState)
            {
                float timeSinceChange = Time.time - stateChangeTime;
                float currentUtility = currentState?.ComputeUtility(context) ?? 0f;
                
                // Apply hysteresis: only switch if:
                // 1. Minimum time has passed, AND
                // 2. The utility difference exceeds the threshold
                bool shouldSwitch = timeSinceChange >= minTimeInState && 
                                   (highestUtility - currentUtility) > utilityThreshold;
                
                if (shouldSwitch)
                {
                    TransitionTo(bestState, context);
                }
            }
        }

        /// <summary>
        /// Force a transition to a specific state
        /// </summary>
        public void ForceTransition(AIState newState, AIContext context)
        {
            if (newState != null && states.Contains(newState))
            {
                TransitionTo(newState, context);
            }
        }

        private void TransitionTo(AIState newState, AIContext context)
        {
            if (newState == currentState) return;
            
            string oldStateName = currentState?.GetType().Name ?? "None";
            string newStateName = newState.GetType().Name;
            
            RLog.AI($"[AIStateMachine] Transitioning from {oldStateName} to {newStateName}");
            
            currentState?.Exit();
            currentState = newState;
            currentState.Enter(context);
            stateChangeTime = Time.time;
        }

        /// <summary>
        /// Get the current state as a specific type
        /// </summary>
        public T GetCurrentState<T>() where T : AIState
        {
            return currentState as T;
        }

        /// <summary>
        /// Check if the current state is of a specific type
        /// </summary>
        public bool IsInState<T>() where T : AIState
        {
            return currentState is T;
        }
    }
} 