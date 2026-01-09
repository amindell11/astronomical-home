#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EnemyAI.States
{
    public partial class AIStateMachine
    {
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

                    var topStates = stateUtilities.OrderByDescending(s => s.utility).Take(3).ToList();
                    var probabilities = ComputeSoftmaxProbabilities(topStates);
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
    }
}
#endif
