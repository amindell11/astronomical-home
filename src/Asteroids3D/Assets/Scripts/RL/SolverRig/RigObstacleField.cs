using AI.Scanning;
using Game;
using UnityEngine;

namespace RL.SolverRig
{
    public sealed class RigObstacleField : IObstacleField
    {
        private readonly RigCircle[] circles;

        public RigObstacleField(RigCircle[] circles) => this.circles = circles;

        public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer)
        {
            var count = 0;
            if (circles == null) return count;
            foreach (var circle in circles)
            {
                if (Mathf.Abs(circle.center.x - centerPlane.x) > halfExtent + circle.radius
                    || Mathf.Abs(circle.center.y - centerPlane.y) > halfExtent + circle.radius) continue;
                if (count == buffer.Length) break;
                buffer[count++] = new DetectedObstacle(GamePlane.PlanePointToWorld(
                    new Vector2(circle.center.x, circle.center.y)), circle.radius, null);
            }
            return count;
        }
    }
}
