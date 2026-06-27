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

            if (settings.IsActive(AIDebugChannel.StateDetail) && CurrentAIState != null && Context != null)
                CurrentAIState.OnDrawGizmos(Context);

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
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var selector = (UtilitySelector)target;
            if (!Application.isPlaying) return;

            var state = selector.CurrentAIState;
            if (state == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Factor Breakdown: {state.ProfileName}", EditorStyles.boldLabel);

            var builder = state.LastBuilder;
            if (builder == null || builder.Factors == null || builder.Factors.Count == 0)
            {
                EditorGUILayout.LabelField("No factor data available.");
                Repaint();
                return;
            }

            EditorGUILayout.LabelField($"Result: {builder.Result:F3} (geomean, {builder.Factors.Count} factors)");
            EditorGUILayout.Space(2);

            foreach (var (name, value) in builder.Factors)
            {
                var color = GetFactorColor(value);
                DrawFactorBar(name, value, color);
            }

            Repaint();
        }

        private void DrawFactorBar(string label, float value, Color color)
        {
            var rect = EditorGUILayout.GetControlRect(false, 18);

            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));

            // Factors are clamped 0.01–2.0; normalize bar to 0–2 range
            var pct = Mathf.Clamp01(value / 2f);
            var barRect = new Rect(rect.x, rect.y, rect.width * pct, rect.height);
            EditorGUI.DrawRect(barRect, color * 0.7f);

            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            EditorGUI.LabelField(rect, $" {label}: {value:F2}", style);
        }

        private static Color GetFactorColor(float value)
        {
            // Below 1.0 drags utility down (red), above 1.0 boosts (green), near 1.0 neutral (yellow)
            if (value < 1f)
                return Color.Lerp(Color.red, Color.yellow, value);
            return Color.Lerp(Color.yellow, Color.green, Mathf.Clamp01(value - 1f));
        }
    }
}
#endif
