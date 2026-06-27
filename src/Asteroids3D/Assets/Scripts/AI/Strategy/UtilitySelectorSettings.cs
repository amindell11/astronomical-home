using UnityEngine;

namespace AI.Utility
{
    /// <summary>
    /// Configuration settings for the StateMachine component.
    /// This ScriptableObject centralizes all state machine tuning parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "UtilitySelectorSettings", menuName = "AI/Utility Selector Settings")]
    public class UtilitySelectorSettings : ScriptableObject
    {
        [Header("Transition Settings")]
        [Tooltip("Minimum time in seconds before switching states")]
        public float minTimeInState = 0.5f;
        
        [Tooltip("Utility difference threshold for state changes")]
        public float utilityThreshold = 0.1f;
        
        [Header("State Selection")]
        [Tooltip("If true, sample states probabilistically instead of always choosing the best")]
        public bool useProbabilisticSampling;
        
        [Tooltip("Controls randomness when probabilistic sampling is enabled. Lower = more deterministic, Higher = more random")]
        [Range(0.1f, 5.0f)]
        public float samplingTemperature = 1.0f;
        
        [Header("Utility Smoothing & Stickiness")]
        [Tooltip("Additive bonus applied to the current state's utility to encourage stability (stickiness)")]
        [Range(0f, 1f)]
        public float stickyBonus = 0.05f;

        [Tooltip("Exponential smoothing factor for rolling average of utility scores. 0 = no smoothing, 1 = no history")]
        [Range(0f, 1f)]
        public float utilitySmoothingFactor = 0.25f;

        [Tooltip("Time in seconds for the sticky bonus to fade to zero (linear fade). 0 = no fade")] 
        public float stickyFadeTime = 2f;
    }
}
