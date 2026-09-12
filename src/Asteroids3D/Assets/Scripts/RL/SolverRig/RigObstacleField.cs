using System;
using System.Collections.Generic;
using AI.Scanning;
using UnityEngine;
using Substrate;

namespace RL.SolverRig
{
    /// <summary>Adapts a scenario's authored circles to the production obstacle-field seam, so the rig's baker path is the ship's own: scanner query, reach cull and nearest-first truncation included.</summary>
    public sealed class RigObstacleField : IObstacleField
    {
        private readonly RigCircle[] circles;
        private readonly List<DetectedObstacle> candidates = new();

        public RigObstacleField(RigCircle[] circles) => this.circles = circles ?? Array.Empty<RigCircle>();

        public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer)
        {
            Gather(centerPlane, halfExtent);
            return ObstacleSelection.KeepNearest(candidates, centerPlane, buffer);
        }

        public int QueryAllObstacles(Vector2 centerPlane, float halfExtent, ref DetectedObstacle[] buffer)
        {
            Gather(centerPlane, halfExtent);
            return ObstacleSelection.CopyAll(candidates, ref buffer);
        }

        private void Gather(Vector2 centerPlane, float halfExtent)
        {
            candidates.Clear();
            foreach (var circle in circles)
            {
                var reach = halfExtent + circle.radius;
                if (Mathf.Abs(circle.center.x - centerPlane.x) > reach || Mathf.Abs(circle.center.y - centerPlane.y) > reach) continue;
                candidates.Add(new DetectedObstacle(
                    GamePlane.PlanePointToWorld(new Vector2(circle.center.x, circle.center.y)),
                    circle.radius, collider: null));
            }
        }
    }
}
