#if UNITY_EDITOR
using AI.Context;
using AI.Debug;
using AI.Utility;
using UnityEditor;
using UnityEngine;

namespace AI
{
    public partial class Brain
    {
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

        private UtilityChooser UtilityChooser => chooser as UtilityChooser;

        // Hot-reload the policy when its config changes in the inspector during play — editing the
        // stateProfiles array here, or (via StateProfile.OnValidate) a profile asset's contents —
        // so a paused session picks up the change instead of staying stuck on the old states.
        private void OnValidate() => RefreshStates();

        /// <summary>Editor-only: rebuild the state set from the current profiles and restart state
        /// selection. No-op before the brain is initialized (i.e. outside play).</summary>
        internal void RefreshStates()
        {
            if (navigator == null) return;
            UtilityChooser?.ResetForTesting();
            BuildStates();
        }

        void OnDrawGizmos() => DrawGizmosImpl(false);
        void OnDrawGizmosSelected() => DrawGizmosImpl(true);

        void DrawGizmosImpl(bool isSelected)
        {
            var settings = CachedSettings;
            if (settings == null || !settings.ShouldDraw(isSelected)) return;

            var uc = UtilityChooser;
            if (uc == null) return;

            if (settings.IsActive(AIDebugChannel.StateDetail) && uc.CurrentAIState != null && uc.Context != null)
                uc.CurrentAIState.OnDrawGizmos(uc.Context);

            if (settings.IsActive(AIDebugChannel.Info) && uc.Context != null)
                DrawInfoLabel(uc.Context);
        }

        private void DrawInfoLabel(AIContext ctx)
        {
            var a = ctx.Assessment;
            Handles.color = Color.white;
            var info = $"HP: {a.HealthPct:P0} Shield: {a.ShieldPct:P0}";
            if (a.NearbyEnemyCount > a.NearbyFriendCount)
                info += $"\nOutnumbered {a.NearbyEnemyCount}v{a.NearbyFriendCount + 1}";
            Handles.Label(transform.position + Vector3.up * 5f, info);
        }
    }

    [CustomEditor(typeof(Brain))]
    public class BrainEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var brain = (Brain)target;
            if (!Application.isPlaying) return;

            var uc = brain.Chooser as UtilityChooser;
            var state = uc?.CurrentAIState;
            if (state == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Factor Breakdown: {state.ProfileName}", EditorStyles.boldLabel);

            var builder = uc.Sampler?.GetBuilder(state);
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
