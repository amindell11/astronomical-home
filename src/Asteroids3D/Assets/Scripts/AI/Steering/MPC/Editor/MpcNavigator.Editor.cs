#if UNITY_EDITOR
using AI;
using AI.Debug;
using AI.Scanning;
using Movement;
using Game;
using UnityEditor;
using UnityEngine;

namespace Movement.MPC
{
    public enum CostBreakdownMode
    {
        CurrentState,
        Trajectory,
    }

    public partial class MpcNavigator
    {
        [Header("Debug Visualization")]
        [Tooltip("Horizontal prediction step for labels")]
        public int labelStep = 5;
        [Tooltip("Show cost breakdown in Inspector")]
        public bool showCostBreakdown = true;
        [Tooltip("Which cost to display: current ship state or full trajectory")]
        public CostBreakdownMode costBreakdownMode = CostBreakdownMode.CurrentState;
        [Tooltip("Log solver performance once per second")]
        public bool logSolverPerformance = false;

        private float nextLogTime;

        private DetectedObstacle[] dbgObstacles;
        private float[] dbgObstacleWeights;
        private int dbgObstacleCount;

        // Debug info
        public CostBreakdown lastCostBreakdown;
        public float lastSolveTimeMs;

        [Header("Scene Gizmo Sub-Toggles")]
        public bool showObstacleCosts = true;
        public bool showTrajectoryCosts = true;

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

        partial void StoreDebugObstacles(ObstacleScan scan)
        {
            if (dbgObstacles == null || dbgObstacles.Length < scan.count)
            {
                var size = Mathf.Max(scan.count, 32);
                dbgObstacles = new DetectedObstacle[size];
                dbgObstacleWeights = new float[size];
            }

            dbgObstacleCount = scan.count;
            var shipMass = dynamics.mass;
            var invShipMass = shipMass > 0f ? 1f / shipMass : 1f;
            for (var i = 0; i < scan.count; i++)
            {
                dbgObstacles[i] = scan.buffer[i];
                var rb = scan.buffer[i].collider ? scan.buffer[i].collider.attachedRigidbody : null;
                dbgObstacleWeights[i] = (rb ? rb.mass : shipMass) * invShipMass;
            }
        }

        private CostBreakdown EvaluateBreakdown(State mpcState)
        {
            var input = solver.BuildCostInput(GoalPos(), GoalVel(), enemyYaw, enemyYawRate, projectileSpeed);
            if (costBreakdownMode == CostBreakdownMode.CurrentState)
                return Cost.EvaluateBreakdown(mpcState, bestSequence[0], lastControl, input, config, false);
            return Cost.EvaluateTrajectoryBreakdown(mpcState, bestSequence, input, config, dynamics, lastControl);
        }

        partial void LogSolverPerformanceIfNeeded()
        {
            if (!logSolverPerformance || !(Time.time > nextLogTime)) return;
            Debug.Log($"[MPC] {gameObject.name} | Solve: {lastSolveTimeMs:F2}ms | Cost: {lastBestCost:F1}");
            nextLogTime = Time.time + 1f;
        }

        private void OnDrawGizmos() => DrawGizmosImpl(false);
        private void OnDrawGizmosSelected() => DrawGizmosImpl(true);

        void DrawGizmosImpl(bool isSelected)
        {
            var settings = CachedSettings;
            if (settings == null || !settings.ShouldDraw(isSelected)) return;
            if (!settings.IsActive(AIDebugChannel.Steering)) return;

            DrawPredictedTrajectory();
            DrawGoal();
            DrawObstacleDebugInfo();
        }

        private void DrawPredictedTrajectory()
        {
            if (predictedStates == null || predictedStates.Length == 0) return;

            var prevPos = GamePlane.PlanePointToWorld(new Vector2(predictedStates[0].pos.x, predictedStates[0].pos.y));
            var prevU = bestSequence[0];
            var input = solver.BuildCostInput(GoalPos(), GoalVel(), enemyYaw, enemyYawRate, projectileSpeed);

            for (var i = 1; i < predictedStates.Length; i++)
            {
                var state = predictedStates[i];
                var u = bestSequence[i];
                var pos = GamePlane.PlanePointToWorld(new Vector2(state.pos.x, state.pos.y));

                var isTerminal = i == predictedStates.Length - 1;
                var stepBreakdown = Cost.EvaluateBreakdown(state, u, prevU, input, config, isTerminal, i);

                Gizmos.color = showTrajectoryCosts ? GetCostColor(stepBreakdown.obstacle / config.wObstacle) : Color.cyan;

                Gizmos.DrawLine(prevPos, pos);
                Gizmos.DrawSphere(pos, 0.15f);

                if (i % labelStep == 0)
                {
                    Handles.Label(pos + Vector3.up * 0.2f,
                        $"Cost: {stepBreakdown.total:F1}\n(P:{stepBreakdown.pos:F1} O:{stepBreakdown.obstacle:F1})",
                        new GUIStyle { normal = { textColor = Color.white }, fontSize = 10 });
                }

                prevPos = pos;
                prevU = u;
            }
        }

        private void DrawGoal()
        {
            if (currentWaypoint.isValid)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(GamePlane.PlanePointToWorld(currentWaypoint.position), arriveRadius);
            }
        }

        private void DrawObstacleDebugInfo()
        {
            if (!showObstacleCosts || dbgObstacles == null || dbgObstacleCount == 0) return;

            for (var i = 0; i < dbgObstacleCount; i++)
            {
                var obs = dbgObstacles[i];
                var weight = dbgObstacleWeights != null && i < dbgObstacleWeights.Length
                    ? dbgObstacleWeights[i] : 1f;
                var obsWorldPos = GamePlane.PlanePointToWorld(obs.position);

                var weightAlpha = Mathf.Clamp01(weight);
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f + 0.7f * weightAlpha);
                Gizmos.DrawWireSphere(obsWorldPos, obs.radius);

                var threshold = obs.radius + settings.obstacleThreshold;
                Gizmos.color = new Color(1f, 1f, 0f, 0.1f + 0.4f * weightAlpha);
                Gizmos.DrawWireSphere(obsWorldPos, threshold);

                DrawObstacleCostField(obs, threshold, weight);
            }
        }

        private void DrawObstacleCostField(DetectedObstacle obstacle, float threshold, float weight)
        {
            var obsWorldPos = GamePlane.PlanePointToWorld(obstacle.position);
            var rings = 5;

            for (var i = 1; i <= rings; i++)
            {
                var radius = obstacle.radius + (threshold - obstacle.radius) * (i / (float)rings);
                var normalizedDist = radius / threshold;
                var epsilon = 0.01f;
                var cost = weight / ((normalizedDist + epsilon) * (normalizedDist + epsilon));
                var visualCost = Mathf.Clamp01(cost / 10f);
                Gizmos.color = new Color(1f, 0f, 0f, visualCost * 0.5f);
                Gizmos.DrawWireSphere(obsWorldPos, radius);
            }
        }

        private Color GetCostColor(float obstacleCost)
        {
            if (obstacleCost < 0.1f)
                return Color.cyan;
            else if (obstacleCost < 1f)
                return Color.Lerp(Color.cyan, Color.yellow, obstacleCost);
            else if (obstacleCost < 5f)
                return Color.Lerp(Color.yellow, Color.red, (obstacleCost - 1f) / 4f);
            else
                return Color.red;
        }
    }

    [CustomEditor(typeof(MpcNavigator))]
    public class MpcNavigatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var nav = (MpcNavigator)target;
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
            var total = breakdown.total;
            EditorGUILayout.LabelField($"Total Cost: {total:F2}");

            DrawCostBar("Position", breakdown.pos, total, Color.green);
            DrawCostBar("Heading", breakdown.heading, total, Color.yellow);
            DrawCostBar("Facing", breakdown.facing, total, Color.cyan);
            DrawCostBar("Velocity", breakdown.vel, total, Color.blue);
            DrawCostBar("Yaw Rate", breakdown.yawRate, total, Color.magenta);
            DrawCostBar("Obstacle", breakdown.obstacle, total, Color.red);
            DrawCostBar("LOS", breakdown.los, total, new Color(1f, 0.5f, 0f));
            DrawCostBar("Exposure", breakdown.exposure, total, new Color(1f, 0.3f, 0.3f));
            DrawCostBar("Tangential", breakdown.tangential, total, new Color(0.3f, 0.8f, 1f));
            DrawCostBar("Effort", breakdown.effort, total, Color.gray);
            DrawCostBar("Smoothness", breakdown.smoothness, total, Color.white);

            Repaint();
        }

        private void DrawCostBar(string label, float value, float total, Color color)
        {
            var pct = total > 0 ? value / total : 0;
            var rect = EditorGUILayout.GetControlRect(false, 18);

            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));

            var barRect = new Rect(rect.x, rect.y, rect.width * pct, rect.height);
            EditorGUI.DrawRect(barRect, color * 0.7f);

            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            EditorGUI.LabelField(rect, $" {label}: {value:F1} ({pct * 100:F0}%)", style);
        }
    }
}
#endif
