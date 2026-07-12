using AI.Context;
using AI.Debug;
using AI.States;
using AI.Utility;
using UnityEditor;
using UnityEngine;

namespace AI
{
    internal static class BrainGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Brain))]
        private static void Draw(Brain brain, GizmoType gizmoType)
        {
            var isSelected = (gizmoType & GizmoType.Selected) != 0;
            var settings = AIDebugContext.Settings;
            if (!settings || !settings.ShouldDraw(isSelected)) return;

            if (brain.Chooser is not UtilityChooser uc || uc.Context == null) return;

            if (settings.IsActive(AIDebugChannel.StateDetail) && uc.CurrentAIState != null)
                GoalRunnerGizmos.Draw(uc.CurrentAIState, uc.Context);

            if (settings.IsActive(AIDebugChannel.Info))
                DrawInfoLabel(brain, uc.Context);
        }

        private static void DrawInfoLabel(Brain brain, AIContext ctx)
        {
            var a = ctx.Assessment;
            Handles.color = Color.white;
            var info = $"HP: {a.HealthPct:P0} Shield: {a.ShieldPct:P0}";
            if (a.NearbyEnemyCount > a.NearbyFriendCount)
                info += $"\nOutnumbered {a.NearbyEnemyCount}v{a.NearbyFriendCount + 1}";
            Handles.Label(brain.transform.position + Vector3.up * 5f, info);
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
