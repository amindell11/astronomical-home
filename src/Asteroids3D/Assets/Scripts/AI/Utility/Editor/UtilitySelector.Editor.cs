#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AI.Debug;
using AI.States;
using UnityEditor;
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

            if (settings.IsActive(AIDebugChannel.Info) && Context != null)
                DrawInfoLabel();
        }

        private void DrawInfoLabel()
        {
            var a = Context.Assessment;
            Handles.color = Color.white;
            var info = $"HP: {a.HealthPct:P0} Shield: {a.ShieldPct:P0}";
            if (a.NearbyEnemyCount > a.NearbyFriendCount)
                info += $"\nOutnumbered {a.NearbyEnemyCount}v{a.NearbyFriendCount + 1}";
            Handles.Label(transform.position + Vector3.up * 5f, info);
        }
    }

    [CustomEditor(typeof(UtilitySelector))]
    public class UtilitySelectorEditor : Editor
    {
        private static readonly Dictionary<StateType, Color> StateColors = new()
        {
            { StateType.Attack, new Color(1f, 0.2f, 0.2f) },
            { StateType.AttackAggressive, new Color(1f, 0.1f, 0.1f) },
            { StateType.AttackEvasive, new Color(1f, 0.6f, 0.2f) },
            { StateType.Evade, new Color(0.2f, 0.9f, 0.3f) },
            { StateType.Patrol, new Color(0.3f, 0.5f, 1f) },
            { StateType.Idle, new Color(0.5f, 0.5f, 0.5f) },
            { StateType.Pursuit, new Color(0.9f, 0.9f, 0.2f) },
        };

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var selector = (UtilitySelector)target;
            if (!Application.isPlaying) return;

            var scores = selector.UtilityScores;
            if (scores == null || scores.Count == 0) return;

            EditorGUILayout.Space();

            var modeLabel = selector.Config != null && selector.Config.useProbabilisticSampling
                ? $"Utility Scores (Probabilistic, T={selector.Config.samplingTemperature:F1})"
                : "Utility Scores (Deterministic)";
            EditorGUILayout.LabelField(modeLabel, EditorStyles.boldLabel);

            var currentType = selector.CurrentState?.Type;
            if (currentType.HasValue)
                EditorGUILayout.LabelField($"Current State: {currentType.Value}");

            EditorGUILayout.Space(2);

            var sorted = scores.OrderByDescending(kv => kv.Value).ToList();
            var maxScore = sorted.Count > 0 ? Mathf.Max(sorted[0].Value, 0.01f) : 1f;

            foreach (var kv in sorted)
            {
                var color = StateColors.TryGetValue(kv.Key, out var c) ? c : Color.grey;
                if (currentType.HasValue && kv.Key == currentType.Value)
                    color = Color.Lerp(color, Color.white, 0.3f);

                DrawUtilityBar(kv.Key.ToString(), kv.Value, maxScore, color, kv.Key == currentType);
            }

            if (selector.Config != null && selector.Config.useProbabilisticSampling && selector.Sampler != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Softmax Probabilities", EditorStyles.boldLabel);

                var stateUtilities = selector.RegisteredStates
                    .Select(s => (state: s, utility: scores.GetValueOrDefault(s.Type, 0f)))
                    .OrderByDescending(s => s.utility)
                    .Take(3)
                    .ToList();

                var probabilities = selector.Sampler.ComputeSoftmaxProbabilities(stateUtilities)
                    .OrderByDescending(p => p.probability)
                    .ToList();

                foreach (var (state, probability) in probabilities)
                {
                    var color = StateColors.TryGetValue(state.Type, out var c) ? c : Color.grey;
                    DrawUtilityBar(state.Type.ToString(), probability, 1f, color, state.Type == currentType);
                }
            }

            Repaint();
        }

        private void DrawUtilityBar(string label, float value, float maxValue, Color color, bool isCurrent)
        {
            var pct = maxValue > 0 ? value / maxValue : 0;
            var rect = EditorGUILayout.GetControlRect(false, 18);

            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));

            var barRect = new Rect(rect.x, rect.y, rect.width * pct, rect.height);
            EditorGUI.DrawRect(barRect, color * 0.7f);

            if (isCurrent)
            {
                var borderRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, borderRect.width, 1), Color.white);
                EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.yMax - 1, borderRect.width, 1), Color.white);
                EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, 1, borderRect.height), Color.white);
                EditorGUI.DrawRect(new Rect(borderRect.xMax - 1, borderRect.y, 1, borderRect.height), Color.white);
            }

            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            EditorGUI.LabelField(rect, $" {label}: {value:F2}", style);
        }
    }
}
#endif
