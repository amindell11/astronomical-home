#if UNITY_EDITOR
using AI.Steering;
using Game;
using UnityEngine;

namespace AI
{
    public partial class MpcNavigator
    {
        [Header("Debug Visualization")]
        [Tooltip("Show debug gizmos in scene view")]
        public bool showDebugGizmos = true;
        [Tooltip("Show obstacle cost field visualization")]
        public bool showObstacleCosts = true;
        [Tooltip("Show predicted trajectory with cost colors")]
        public bool showTrajectoryCosts = true;

        private ObstacleData dbgObstacles;

        partial void StoreDebugObstacles(ObstacleData obstacles)
        {
            // Deep copy obstacle data for visualization
            if (dbgObstacles == null || dbgObstacles.obstacles.Length < obstacles.count)
            {
                dbgObstacles = new ObstacleData(obstacles.obstacles.Length);
            }
            dbgObstacles.Clear();
            for (var i = 0; i < obstacles.count; i++)
            {
                dbgObstacles.Add(obstacles.obstacles[i].position, obstacles.obstacles[i].radius);
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

            var prevPos = GamePlane.PlanePointToWorld(predictedStates[0].pos);
            for (var i = 1; i < predictedStates.Length; i++)
            {
                var state = predictedStates[i];
                var pos = GamePlane.PlanePointToWorld(state.pos);

                if (showTrajectoryCosts && dbgObstacles != null)
                {
                    var obstacleCost = EvaluateObstacleCostForState(state.pos);
                    Gizmos.color = GetCostColor(obstacleCost);
                }
                else
                {
                    Gizmos.color = Color.cyan;
                }

                Gizmos.DrawLine(prevPos, pos);
                Gizmos.DrawSphere(pos, 0.15f);
                prevPos = pos;
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
            if (!showObstacleCosts || dbgObstacles == null || dbgObstacles.count == 0) return;

            for (var i = 0; i < dbgObstacles.count; i++)
            {
                var obs = dbgObstacles.obstacles[i];
                var obsWorldPos = GamePlane.PlanePointToWorld(obs.position);
                
                // Draw obstacle itself (white)
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(obsWorldPos, obs.radius);
                
                // Draw cost threshold radius (yellow)
                var threshold = obs.radius + obstacleThreshold;
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawWireSphere(obsWorldPos, threshold);
                
                // Draw cost field gradient (red rings)
                DrawObstacleCostField(obs, threshold);
            }
        }

        private void DrawObstacleCostField(ObstacleInfo obstacle, float threshold)
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
            if (dbgObstacles == null || dbgObstacles.count == 0) return 0f;

            var cost = 0f;
            for (var i = 0; i < dbgObstacles.count; i++)
            {
                var obstacle = dbgObstacles.obstacles[i];
                var dist = Vector2.Distance(pos, obstacle.position);
                var threshold = obstacle.radius + obstacleThreshold;

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
}
#endif
