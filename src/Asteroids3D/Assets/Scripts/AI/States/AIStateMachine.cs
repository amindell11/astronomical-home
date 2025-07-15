using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// State machine component that manages AI states based on utility scores.
    /// Handles state transitions with hysteresis to prevent thrashing.
    /// </summary>
    public class AIStateMachine : MonoBehaviour
    {
        [System.Serializable]
        public class StateWeights
        {
            [Header("State Utility Weights")]
            [Tooltip("Weight multiplier for Idle state utility")]
            [Range(0f, 2f)] public float idleWeight = 1f;
            
            [Tooltip("Weight multiplier for Patrol state utility")]
            [Range(0f, 2f)] public float patrolWeight = 1f;
            
            [Tooltip("Weight multiplier for Attack state utility")]
            [Range(0f, 2f)] public float attackWeight = 1f;
            
            [Tooltip("Weight multiplier for Evade state utility")]
            [Range(0f, 2f)] public float evadeWeight = 1f;
            
            [Tooltip("Weight multiplier for Kite state utility")]
            [Range(0f, 2f)] public float kiteWeight = 1f;
        }
        
        [Header("State Configuration")]
        [Tooltip("Weights for adjusting state utility calculations")]
        public StateWeights stateWeights = new StateWeights();
        
        [Header("Transition Settings")]
        [Tooltip("Minimum time in seconds before switching states")]
        [SerializeField] private float minTimeInState = 0.5f;
        
        [Tooltip("Utility difference threshold for state changes")]
        [SerializeField] private float utilityThreshold = 0.1f;
        
        [Header("State Selection")]
        [Tooltip("If true, sample states probabilistically instead of always choosing the best")]
        public bool useProbabilisticSampling = false;
        
        [Tooltip("Controls randomness when probabilistic sampling is enabled. Lower = more deterministic, Higher = more random")]
        [Range(0.1f, 5.0f)]
        public float samplingTemperature = 1.0f;
        
        [Header("Debug")]
        [Tooltip("Show state selection debug info (utilities/probabilities) in scene view")]
        public bool showStateSelectionGizmos = true;
        
        [Tooltip("Show current state debug gizmos in scene view")]
        public bool showCurrentStateGizmos = true;
        
        private readonly List<AIState> states = new List<AIState>();
        private AIState currentState;
        private float stateChangeTime;
        private AIContext aiContext;
        
        public AIState CurrentState => currentState;
        public string CurrentStateName => currentState?.GetType().Name ?? "None";
        
        /// <summary>
        /// Dictionary of utility scores for debugging
        /// </summary>
        public Dictionary<string, float> UtilityScores { get; private set; } = new Dictionary<string, float>();

        void Awake()
        {
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
            this.aiContext = context;
            if (states.Count == 0) return;
            
            // Update current state
            currentState?.Tick(context, deltaTime);
            
            // Evaluate utilities for all states
            UtilityScores.Clear();
            AIState bestState = null;
            float highestUtility = -1f;
            
            var stateUtilities = new List<(AIState state, float utility)>();
            
            foreach (var state in states)
            {
                float baseUtility = state.ComputeUtility(context);
                float weightedUtility = ApplyStateWeight(state, baseUtility);
                UtilityScores[state.GetType().Name] = weightedUtility;
                
                stateUtilities.Add((state, weightedUtility));
                
                if (weightedUtility > highestUtility)
                {
                    highestUtility = weightedUtility;
                    bestState = state;
                }
            }
            
            // Select state using either deterministic or probabilistic method
            AIState selectedState;
            if (useProbabilisticSampling)
            {
                selectedState = SampleStateFromDistribution(stateUtilities);
            }
            else
            {
                selectedState = bestState;
            }
            
            // Check if we should transition
            if (selectedState != null && selectedState != currentState)
            {
                float timeSinceChange = Time.time - stateChangeTime;
                float currentUtility = currentState?.ComputeUtility(context) ?? 0f;
                float weightedCurrentUtility = ApplyStateWeight(currentState, currentUtility);
                
                bool shouldSwitch;
                if (useProbabilisticSampling)
                {
                    // For probabilistic sampling, only enforce minimum time constraint
                    shouldSwitch = timeSinceChange >= minTimeInState;
                }
                else
                {
                    // For deterministic selection, use both time and utility threshold
                    float selectedUtility = selectedState.ComputeUtility(context);
                    float weightedSelectedUtility = ApplyStateWeight(selectedState, selectedUtility);
                    shouldSwitch = timeSinceChange >= minTimeInState && 
                                   (weightedSelectedUtility - weightedCurrentUtility) > utilityThreshold;
                }
                
                if (shouldSwitch)
                {
                    TransitionTo(selectedState, context);
                }
            }
        }

        /// <summary>
        /// Apply state weight to utility score
        /// </summary>
        private float ApplyStateWeight(AIState state, float baseUtility)
        {
            if (state == null) return baseUtility;
            
            string stateName = state.GetType().Name;
            return stateName switch
            {
                "IdleState" => baseUtility * stateWeights.idleWeight,
                "PatrolState" => baseUtility * stateWeights.patrolWeight,
                "AttackState" => baseUtility * stateWeights.attackWeight,
                "EvadeState" => baseUtility * stateWeights.evadeWeight,
                "KiteState" => baseUtility * stateWeights.kiteWeight,
                _ => baseUtility
            };
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
        
        /// <summary>
        /// Sample a state from the utility distribution using softmax and temperature
        /// </summary>
        private AIState SampleStateFromDistribution(List<(AIState state, float utility)> stateUtilities)
        {
            if (stateUtilities.Count == 0) return null;
            
            // Compute softmax probabilities
            var probabilities = ComputeSoftmaxProbabilities(stateUtilities);
            
            // Sample from the distribution
            return SampleFromProbabilities(probabilities);
        }
        
        /// <summary>
        /// Convert utilities to probabilities using softmax with temperature
        /// </summary>
        private List<(AIState state, float probability)> ComputeSoftmaxProbabilities(List<(AIState state, float utility)> stateUtilities)
        {
            var probabilities = new List<(AIState state, float probability)>();
            
            // Apply temperature and find max for numerical stability
            float maxUtility = float.MinValue;
            foreach (var (state, utility) in stateUtilities)
            {
                float temperedUtility = utility / samplingTemperature;
                if (temperedUtility > maxUtility)
                    maxUtility = temperedUtility;
            }
            
            // Compute exponentials and sum
            float expSum = 0f;
            var expValues = new List<(AIState state, float exp)>();
            
            foreach (var (state, utility) in stateUtilities)
            {
                float temperedUtility = utility / samplingTemperature;
                float exp = Mathf.Exp(temperedUtility - maxUtility); // Subtract max for numerical stability
                expValues.Add((state, exp));
                expSum += exp;
            }
            
            // Normalize to get probabilities
            foreach (var (state, exp) in expValues)
            {
                float probability = expSum > 0f ? exp / expSum : 1f / expValues.Count;
                probabilities.Add((state, probability));
            }
            
            return probabilities;
        }
        
        /// <summary>
        /// Sample a state from probability distribution
        /// </summary>
        private AIState SampleFromProbabilities(List<(AIState state, float probability)> probabilities)
        {
            if (probabilities.Count == 0) return null;
            
            float random = Random.Range(0f, 1f);
            float cumulativeProbability = 0f;
            
            foreach (var (state, probability) in probabilities)
            {
                cumulativeProbability += probability;
                if (random <= cumulativeProbability)
                {
                    return state;
                }
            }
            
            // Fallback to last state if floating point errors occur
            return probabilities[probabilities.Count - 1].state;
        }
        
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!showCurrentStateGizmos && !showStateSelectionGizmos) return;
            
            Vector3 pos = transform.position;
            
            // Draw current state gizmos
            if (showCurrentStateGizmos && currentState != null && aiContext != null)
            {
                currentState.OnDrawGizmos(aiContext);
            }
            
            // Draw utility/probability scores
            if (showStateSelectionGizmos && UtilityScores != null && UtilityScores.Count > 0)
            {
                UnityEditor.Handles.color = Color.white;
                
                string headerText;
                List<string> scoreLines;
                
                if (useProbabilisticSampling)
                {
                    // Show probabilities when probabilistic sampling is enabled
                    headerText = $"Current State: {CurrentStateName} (Probabilistic, T={samplingTemperature:F1})";
                    
                    // Compute probabilities for display
                    var stateUtilities = new List<(AIState state, float utility)>();
                    foreach (var state in states)
                    {
                        if (UtilityScores.TryGetValue(state.GetType().Name, out float utility))
                        {
                            stateUtilities.Add((state, utility));
                        }
                    }
                    
                    var probabilities = ComputeSoftmaxProbabilities(stateUtilities);
                    scoreLines = probabilities
                        .OrderByDescending(p => p.probability)
                        .Take(5)
                        .Select(p => $"{p.state.GetType().Name}: {p.probability:P1}")
                        .ToList();
                    
                    headerText += "\nProbabilities:";
                }
                else
                {
                    // Show utilities when deterministic
                    headerText = $"Current State: {CurrentStateName} (Deterministic)";
                    scoreLines = UtilityScores
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(5)
                        .Select(kvp => $"{kvp.Key}: {kvp.Value:F2}")
                        .ToList();
                    
                    headerText += "\nWeighted Utilities:";
                }

                var displayText = headerText + "\n" + string.Join("\n", scoreLines);
                UnityEditor.Handles.Label(pos + Vector3.up * 3f, displayText);
            }
        }
#endif
    }
} 