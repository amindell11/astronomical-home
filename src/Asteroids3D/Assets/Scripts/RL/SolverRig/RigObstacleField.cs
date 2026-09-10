using System.Collections.Generic;
using AI.Scanning;
using Game;
using UnityEngine;

namespace RL.SolverRig
{
    public sealed class RigObstacleField : IObstacleField
    {
        private readonly RigCircle[] circles;
        private readonly List<DetectedObstacle> candidates = new();

        public RigObstacleField(RigCircle[] circles) => this.circles = circles;

        public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer)
        {
            candidates.Clear();
            if (circles == null) return 0;
            foreach (var circle in circles)
            {
                if (Mathf.Abs(circle.center.x - centerPlane.x) > halfExtent + circle.radius
                    || Mathf.Abs(circle.center.y - centerPlane.y) > halfExtent + circle.radius) continue;
                candidates.Add(new DetectedObstacle(GamePlane.PlanePointToWorld(
                    new Vector2(circle.center.x, circle.center.y)), circle.radius, null));
            }
            return ObstacleSelection.KeepNearest(candidates, centerPlane, buffer);
        }
    }
}
