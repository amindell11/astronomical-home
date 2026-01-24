#if UNITY_EDITOR
using AI.Steering;
using Game;
using UnityEditor;
using UnityEngine;

namespace AI.Steering.MPC
{
    public partial class MpcNavigator
    {
        [Header("Debug Visualization")]
        [Tooltip("Horizontal prediction step for labels")]
        public int labelStep = 5;
        [Tooltip("Show cost breakdown in Inspector")]
        public bool showCostBreakdown = true;
        [Tooltip("Log solver performance once per second")]
        public bool logSolverPerformance = false;

        private float nextLogTime;

        private Scanning.DetectedObstacle[] dbgObstacles;
        private int dbgObstacleCount;

        // Debug info
        public CostBreakdown lastCostBreakdown;
        public float lastSolveTimeMs;
        
        [Header("Debug Visualization")]
        public bool showDebugGizmos = true;
        public bool showObstacleCosts = true;
        public bool showTrajectoryCosts = true;

        partial void StoreDebugObstacles(Scanning.ObstacleScan scan)
        {
            // Deep copy obstacle data for visualization
            if (dbgObstacles == null || dbgObstacles.Length < scan.count)
            {
                dbgObstacles = new Scanning.DetectedObstacle[Mathf.Max(scan.count, 32)];
            }
            
            dbgObstacleCount = scan.count;
            for (var i = 0; i < scan.count; i++)
            {
                dbgObstacles[i] = scan.buffer[i];
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            DrawPredictedTrajectory();
            DrawGoal();
            DrawObstacleDebugInfo();
        }

        private void DrawPredictedTrajectory()
        {
            if (predictedStates == null || predictedStates.Length == 0) return;

            if (logSolverPerformance && Time.time > nextLogTime)
            {
                Debug.Log($"[MPC] {gameObject.name} | Solve: {lastSolveTimeMs:F2}ms | Cost: {lastBestCost:F1}");
                nextLogTime = Time.time + 1f;
            }

            var prevPos = GamePlane.PlanePointToWorld(predictedStates[0].pos);
            var prevU = bestSequence[0];

            for (var i = 1; i < predictedStates.Length; i++)
            {
                var state = predictedStates[i];
                var u = bestSequence[i];
                var pos = GamePlane.PlanePointToWorld(state.pos);

                var isTerminal = i == predictedStates.Length - 1;
                var stepBreakdown = Cost.EvaluateBreakdown(state, u, prevU, currentWaypoint.position, 
                    new Scanning.ObstacleScan(dbgObstacles, dbgObstacleCount), config, isTerminal);

                Gizmos.color = showTrajectoryCosts ? GetCostColor(stepBreakdown.obstacle / config.wObstacle) : Color.cyan;

                Gizmos.DrawLine(prevPos, pos);
                Gizmos.DrawSphere(pos, 0.15f);

                if (i % labelStep == 0)
                {
                    UnityEditor.Handles.Label(pos + Vector3.up * 0.2f, 
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
                var obsWorldPos = GamePlane.PlanePointToWorld(obs.position);
                
                // Draw obstacle itself (white)
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(obsWorldPos, obs.radius);
                
                // Draw cost threshold radius (yellow)
                var threshold = obs.radius + settings.obstacleThreshold;
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(obsWorldPos, threshold);
                
                // Draw cost field gradient (red rings)
                DrawObstacleCostField(obs, threshold);
            }
        }

        private void DrawObstacleCostField(Scanning.DetectedObstacle obstacle, float threshold)
        {
            var obsWorldPos = GamePlane.PlanePointToWorld(obstacle.position);
            var rings = 5;
            
            for (var i = 1; i <= rings; i++)
            {
                var radius = obstacle.radius + (threshold - obstacle.radius) * (i / (float)rings);
                var normalizedDist = radius / threshold;
                
                // Inverse square cost (matches MpcController)
                var epsilon = 0.01f;
                var cost = 1f / ((normalizedDist + epsilon) * (normalizedDist + epsilon));
                
                // Normalize cost for visualization (clamped 0-1)
                var visualCost = Mathf.Clamp01(cost / 10f);
                
                // Color from red (high cost) to transparent (low cost)
                Gizmos.color = new Color(1f, 0f, 0f, visualCost * 0.5f);
                Gizmos.DrawWireSphere(obsWorldPos, radius);
            }
        }

        private float EvaluateObstacleCostForState(Vector2 pos)
        {
            if (dbgObstacles == null || dbgObstacleCount == 0) return 0f;

            var cost = 0f;
            for (var i = 0; i < dbgObstacleCount; i++)
            {
                var obstacle = dbgObstacles[i];
                var dist = Vector2.Distance(pos, obstacle.position);
                var threshold = obstacle.radius + settings.obstacleThreshold;

                if (dist < threshold)
                {
                    var normalizedDist = dist / threshold;
                    
                    // Inverse square cost (matches MpcController)
                    var epsilon = 0.01f;
                    cost += 1f / ((normalizedDist + epsilon) * (normalizedDist + epsilon));
                }
            }
            return cost;
        }

        private Color GetCostColor(float obstacleCost)
        {
            // Gradient from cyan (no cost) to red (high cost)
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

    [UnityEditor.CustomEditor(typeof(MpcNavigator))]
    public class MpcNavigatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var nav = (MpcNavigator)target;
            if (!nav.showCostBreakdown || !Application.isPlaying) return;

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("MPC Debug Scaffolding", UnityEditor.EditorStyles.boldLabel);
            
            var solveTimeColor = nav.lastSolveTimeMs > 2.0f ? "red" : "lime";
            UnityEditor.EditorGUILayout.LabelField($"Solve Time: <color={solveTimeColor}>{nav.lastSolveTimeMs:F2} ms</color>", 
                new GUIStyle(UnityEditor.EditorStyles.label) { richText = true });
            
            UnityEditor.EditorGUILayout.LabelField($"Total Cost: {nav.lastBestCost:F2}");

            var breakdown = nav.lastCostBreakdown;
            DrawCostBar("Position", breakdown.pos, nav.lastBestCost, Color.green);
            DrawCostBar("Heading", breakdown.heading, nav.lastBestCost, Color.yellow);
            DrawCostBar("Facing", breakdown.facing, nav.lastBestCost, Color.cyan);
            DrawCostBar("Velocity", breakdown.vel, nav.lastBestCost, Color.blue);
            DrawCostBar("Yaw Rate", breakdown.yawRate, nav.lastBestCost, Color.magenta);
            DrawCostBar("Obstacle", breakdown.obstacle, nav.lastBestCost, Color.red);
            DrawCostBar("Effort", breakdown.effort, nav.lastBestCost, Color.gray);
            DrawCostBar("Smoothness", breakdown.smoothness, nav.lastBestCost, Color.white);

            Repaint();
        }

        private void DrawCostBar(string label, float value, float total, Color color)
        {
            var pct = total > 0 ? value / total : 0;
            var rect = UnityEditor.EditorGUILayout.GetControlRect(false, 18);
            
            // Background
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
            
            // Bar
            var barRect = new Rect(rect.x, rect.y, rect.width * pct, rect.height);
            EditorGUI.DrawRect(barRect, color * 0.7f);
            
            // Text
            var style = new GUIStyle(UnityEditor.EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            EditorGUI.LabelField(rect, $" {label}: {value:F1} ({pct*100:F0}%)", style);
        }
    }
}
#endif
