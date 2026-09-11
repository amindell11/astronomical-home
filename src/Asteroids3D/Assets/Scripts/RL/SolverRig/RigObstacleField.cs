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
            GatherCandidates(centerPlane, halfExtent);
            return ObstacleSelection.KeepNearest(candidates, centerPlane, buffer);
        }

        public int QueryAllObstacles(Vector2 centerPlane, float halfExtent, ref DetectedObstacle[] buffer)
        {
            GatherCandidates(centerPlane, halfExtent);
            return ObstacleSelection.CopyAll(candidates, ref buffer);
        }

        private void GatherCandidates(Vector2 centerPlane, float halfExtent)
        {
            candidates.Clear();
            if (circles == null) return;
            foreach (var circle in circles)
            {
                if (Mathf.Abs(circle.center.x - centerPlane.x) > halfExtent + circle.radius
                    || Mathf.Abs(circle.center.y - centerPlane.y) > halfExtent + circle.radius) continue;
                candidates.Add(new DetectedObstacle(GamePlane.PlanePointToWorld(
                    new Vector2(circle.center.x, circle.center.y)), circle.radius, null));
            }
        }
    }
}
