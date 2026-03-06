#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AI.Debug;
using AI.States;
using UnityEngine;

namespace AI.Utility
{
    public partial class UtilitySelector
    {
        internal Sampler Sampler => sampler;

        private AICommander cachedCommander;
        private AIDebugSettings CachedSettings
        {
            get
            {
                if (!cachedCommander)
                    cachedCommander = GetComponent<AICommander>();
                return cachedCommander ? cachedCommander.DebugSettings : null;
            }
        }

        void OnDrawGizmos() => DrawGizmosImpl(false);
        void OnDrawGizmosSelected() => DrawGizmosImpl(true);

        void DrawGizmosImpl(bool isSelected)
        {
            var settings = CachedSettings;
            if (settings == null || !settings.ShouldDraw(isSelected)) return;

            if (settings.IsActive(AIDebugChannel.StateDetail) && CurrentState != null && Context != null)
                CurrentState.OnDrawGizmos(Context);

            if (settings.IsActive(AIDebugChannel.Utility) && UtilityScores is { Count: > 0 })
                DrawUtilityGizmos();

            if (settings.IsActive(AIDebugChannel.Info) && Context != null)
                DrawInfoLabel();
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

        private void DrawInfoLabel()
        {
            var a = Context.Assessment;
            UnityEditor.Handles.color = Color.white;
            var info = $"HP: {a.HealthPct:P0} Shield: {a.ShieldPct:P0}";
            if (a.NearbyEnemyCount > a.NearbyFriendCount)
                info += $"\nOutnumbered {a.NearbyEnemyCount}v{a.NearbyFriendCount + 1}";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 5f, info);
        }

        private IEnumerable<string> GetUtilityLines() =>
            Enumerable
                .OrderByDescending<KeyValuePair<StateType, float>, float>(UtilityScores, kvp => kvp.Value)
                .Take(5)
                .Select(kvp => $"{kvp.Key}: {kvp.Value:F2}");

        private IEnumerable<string> GetProbabilityLines()
        {
            if (Sampler == null) return Enumerable.Empty<string>();

            var stateUtilities = RegisteredStates
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
