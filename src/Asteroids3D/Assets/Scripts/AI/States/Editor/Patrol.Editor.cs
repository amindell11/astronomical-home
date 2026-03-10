#if UNITY_EDITOR
using Game;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Patrol
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);

            if (ctx == null) return;

            var position = ctx.ShipInfo.Pos3D;

            // Draw patrol radius
            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(position, utilityTuning.patrol.radius);

            // Draw current patrol target if we have one
            if (hasTarget)
            {
                // Convert plane coordinates to world coordinates for rendering
                var currentTargetWorld = GamePlane.PlanePointToWorld(currentTarget);

                // Line to target
                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, currentTargetWorld);

                // Target marker – radius matches the arrive radius
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTargetWorld, utilityTuning.patrol.arriveRadius);
                Gizmos.DrawWireCube(currentTargetWorld, Vector3.one * 0.5f);

                var distToTarget = Vector2.Distance(ctx.ShipInfo.Pos, currentTarget);
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(currentTargetWorld + Vector3.up, $"Patrol Target\n{distToTarget:F1}m");
            }

            // Show patrol state info
            UnityEditor.Handles.color = Color.white;
            var info = $"PATROL\nRadius: {utilityTuning.patrol.radius:F0}m";
            if (hasTarget)
                info += "\nMoving to waypoint";
            else
                info += "\nSearching for waypoint";
            UnityEditor.Handles.Label(position + Vector3.up * 4f, info);
        }
    }
}
#endif
