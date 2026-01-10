#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AI.States;
using UnityEngine;

namespace AI.Utility
{
    public partial class UtilitySelector
    {
        // Exposed for Editor gizmos
        internal IReadOnlyList<AI.States.State> States => states;
        internal Sampler Sampler => sampler;
        
        void OnDrawGizmos()
        {
            if (Config == null) return;
            if (!Config.showCurrentStateGizmos && !Config.showStateSelectionGizmos) return;

            if (Config.showCurrentStateGizmos && CurrentState != null && Context != null)
                CurrentState.OnDrawGizmos(Context);

            if (Config.showStateSelectionGizmos && UtilityScores is { Count: > 0 })
                DrawUtilityGizmos();
        }

        private void DrawUtilityGizmos()
        {
            UnityEditor.Handles.color = Color.white;

            var header = Config.useProbabilisticSampling
                ? $"Current State: {CurrentStateName} (Probabilistic, T={Config.samplingTemperature:F1})\nProbabilities:"
                : $"Current State: {CurrentStateName} (Deterministic)\nWeighted Utilities:";

            var lines = Config.useProbabilisticSampling
                ? GetProbabilityLines()
                : GetUtilityLines();

            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, header + "\n" + string.Join("\n", lines));
        }

        private IEnumerable<string> GetUtilityLines() =>
            Enumerable
                .OrderByDescending<KeyValuePair<StateType, float>, float>(UtilityScores, kvp => kvp.Value)
                .Take(5)
                .Select(kvp => $"{kvp.Key}: {kvp.Value:F2}");

        private IEnumerable<string> GetProbabilityLines()
        {
            if (Sampler == null) return Enumerable.Empty<string>();

            var stateUtilities = States
                .Select(s => (state: s, utility: CollectionExtensions.GetValueOrDefault(UtilityScores, s.Type, 0f)))
                .OrderByDescending(s => s.utility)
                .Take(3)
                .ToList();

            return Sampler.ComputeSoftmaxProbabilities(stateUtilities)
                .OrderByDescending(p => p.probability)
                .Take(5)
                .Select(p => $"{p.state.Type}: {p.probability:P1}");
        }
    }
}
#endif
