using Game;
using UnityEditor;
using UnityEngine;

namespace Movement.MPC
{
    [CustomEditor(typeof(Navigator))]
    public class NavigatorEditor : Editor
    {
        private bool showUnweightedCosts;

        private void OnSceneGUI()
        {
            if (!Application.isPlaying) return;
            if (DrawCandidateSelectionHandles((Navigator)target))
                Repaint();
        }

        // Lives in OnSceneGUI, not the gizmo pass: Handles.Button needs SceneView input access.
        private static bool DrawCandidateSelectionHandles(Navigator nav)
        {
            if (!nav.showCandidateTrajectories || nav.solver == null) return false;
            if (nav.visibleCandidateIndices == null || nav.visibleCount == 0) return false;

            var horizon = nav.solver.LastHorizon;
            if (horizon == 0) return false;
            var candidates = nav.solver.Candidates;
            var initial = nav.lastInitialState;
            var changed = false;

            for (var i = 0; i < nav.visibleCount; i++)
            {
                var idx = nav.visibleCandidateIndices[i];
                var current = initial;
                for (var step = 0; step < horizon; step++)
                    current = Model.Step(current, candidates[idx * horizon + step], nav.config, nav.dynamics);

                var world = GamePlane.PlanePointToWorld(new Vector2(current.pos.x, current.pos.y));
                var size = HandleUtility.GetHandleSize(world) * 0.05f;
                var isSelected = idx == nav.selectedCandidateIndex;
                Handles.color = isSelected ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.55f);
                if (Handles.Button(world, Quaternion.identity, size, size * 1.6f, Handles.DotHandleCap))
                {
                    nav.selectedCandidateIndex = isSelected ? -1 : idx;
                    changed = true;
                }
            }
            return changed;
        }

        private static CostBreakdown? GetSelectedCandidateBreakdown(Navigator nav)
        {
            if (nav.selectedCandidateIndex < 0 || nav.solver == null) return null;
            var samples = nav.solver.LastSampleCount;
            var horizon = nav.solver.LastHorizon;
            if (nav.selectedCandidateIndex >= samples || horizon == 0) return null;

            var candidates = nav.solver.Candidates;
            var seq = new Control[horizon];
            for (var i = 0; i < horizon; i++)
                seq[i] = candidates[nav.selectedCandidateIndex * horizon + i];

            var input = nav.solver.BuildCostInput(nav.GoalPos(), nav.GoalVel(),
                nav.enemyPos, nav.enemyVel, nav.enemyYaw, nav.enemyYawRate,
                nav.projectileSpeed, nav.lastInitialState.vel);
            return Cost.EvaluateTrajectoryBreakdown(nav.lastInitialState, seq, input, nav.config, nav.dynamics, nav.lastControl);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var nav = (Navigator)target;
            if (!nav.showCostBreakdown || !Application.isPlaying) return;

            EditorGUILayout.Space();
            var modeLabel = nav.costBreakdownMode == CostBreakdownMode.CurrentState
                ? "MPC Cost Breakdown (Current State)"
                : "MPC Cost Breakdown (Full Trajectory)";
            EditorGUILayout.LabelField(modeLabel, EditorStyles.boldLabel);

            var solveTimeColor = nav.lastSolveTimeMs > 2.0f ? "red" : "lime";
            EditorGUILayout.LabelField($"Solve Time: <color={solveTimeColor}>{nav.lastSolveTimeMs:F2} ms</color>",
                new GUIStyle(EditorStyles.label) { richText = true });

            var breakdown = nav.lastCostBreakdown;
            EditorGUILayout.LabelField($"Total Cost: {breakdown.total:F2}");

            var horizon = nav.mpcSettings.Horizon;
            var normalizedCost = horizon > 0 ? nav.lastBestCost / horizon : nav.lastBestCost;
            EditorGUILayout.LabelField($"Normalized Cost (per-step): {normalizedCost:F3}");

            showUnweightedCosts = EditorGUILayout.ToggleLeft(
                "Show Unweighted (raw cost / weight)", showUnweightedCosts);

            RenderBreakdownBars(nav.mpcSettings, breakdown);

            if (nav.showCandidateTrajectories && nav.selectedCandidateIndex >= 0)
            {
                EditorGUILayout.Space();
                var selBreakdown = GetSelectedCandidateBreakdown(nav);
                if (selBreakdown.HasValue)
                {
                    var sb = selBreakdown.Value;
                    EditorGUILayout.LabelField(
                        $"Selected Candidate #{nav.selectedCandidateIndex} (trajectory total)",
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Cost: {sb.total:F2}");
                    if (GUILayout.Button("Clear Selection", GUILayout.Width(120)))
                        nav.selectedCandidateIndex = -1;
                    RenderBreakdownBars(nav.mpcSettings, sb);
                }
            }

            Repaint();
        }

        private void RenderBreakdownBars(MpcSettings s, CostBreakdown breakdown)
        {
            var total = breakdown.total;
            DrawCostBar("Position", breakdown.pos, s.wPos, total, Color.green);
            DrawCostBar("Velocity Track", breakdown.velocityTrack, s.wVelTrack, total, new Color(0.5f, 1f, 0.5f));
            DrawCostBar("Heading", breakdown.heading, s.wYaw, total, Color.yellow);
            DrawCostBar("Facing", breakdown.facing, s.wFacing, total, Color.cyan);
            DrawCostBar("Velocity", breakdown.vel, s.wVel, total, Color.blue);
            DrawCostBar("Closing", breakdown.closing, s.wClosing, total, new Color(0.4f, 0.9f, 0.7f));
            DrawCostBar("Yaw Rate", breakdown.yawRate, s.wYawRate, total, Color.magenta);
            DrawCostBar("Obstacle", breakdown.obstacle, s.wObstacle, total, Color.red);
            DrawCostBar("Collision", breakdown.collision, s.collisionPenalty, total, new Color(1f, 0f, 0.5f));
            DrawCostBar("LOS", breakdown.los, s.wLos, total, new Color(1f, 0.5f, 0f));
            DrawCostBar("Exposure", breakdown.exposure, s.wExposure, total, new Color(1f, 0.3f, 0.3f));
            DrawCostBar("Tangential", breakdown.tangential, s.wTangential, total, new Color(0.3f, 0.8f, 1f));
            DrawCostBar("Miss Distance", breakdown.missDistance, s.wMissDistance, total, new Color(1f, 0.8f, 0.2f));
            DrawCostBar("Momentum", breakdown.momentum, s.wMomentum, total, new Color(0.6f, 1f, 0.6f));
            DrawCostBar("Effort", breakdown.effort, s.wEffort, total, Color.gray);
            DrawCostBar("Boost Effort", breakdown.boostEffort, s.wBoostEffort, total, new Color(1f, 0.6f, 0f));
            DrawCostBar("Smoothness", breakdown.smoothness, 0f, total, Color.white);
        }

        private void DrawCostBar(string label, float value, float weight, float total, Color color)
        {
            // Bar magnitude / percentage always reflect the WEIGHTED contribution, so rows stay comparable regardless of toggle state.
            var pct = total > 0 ? value / total : 0;
            var rect = EditorGUILayout.GetControlRect(false, 18);

            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));

            var barWidth = Mathf.Max(rect.width * Mathf.Abs(pct), Mathf.Abs(value) > 1e-6f ? 3f : 0f);
            var barRect = new Rect(rect.x, rect.y, barWidth, rect.height);
            EditorGUI.DrawRect(barRect, color * 0.7f);

            var displayValue = (showUnweightedCosts && weight > 1e-6f) ? value / weight : value;
            var suffix = (showUnweightedCosts && weight > 1e-6f) ? $" ×{weight:G3}" : "";
            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            var absDisp = Mathf.Abs(displayValue);
            var valueStr = absDisp >= 10f ? $"{displayValue:F1}"
                : absDisp >= 1f ? $"{displayValue:F2}"
                : $"{displayValue:F3}";
            EditorGUI.LabelField(rect, $" {label}: {valueStr}{suffix} ({pct * 100:F1}%)", style);
        }
    }
}
