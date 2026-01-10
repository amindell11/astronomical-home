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
            
            if (!ctx) return;
            
            Vector3 position = ctx.SelfPosition3D;
            
            // Draw patrol radius
            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(position, utilityTuning.patrolRadius);
            
            // Draw current patrol target if we have one
            if (hasTarget)
            {
                // Convert plane coordinates to world coordinates for rendering
                Vector3 currentTargetWorld = GamePlane.PlanePointToWorld(currentTarget);
                
                // Line to target
                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, currentTargetWorld);
                
                // Target marker
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTargetWorld, 1.5f);
                Gizmos.DrawWireCube(currentTargetWorld, Vector3.one * 0.5f);
                
                float distToTarget = Vector2.Distance(ctx.SelfPosition, currentTarget);
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(currentTargetWorld + Vector3.up, $"Patrol Target\n{distToTarget:F1}m");
            }
            
            // Show patrol state info
            UnityEditor.Handles.color = Color.white;
            string info = $"PATROL\nRadius: {utilityTuning.patrolRadius:F0}m";
            if (hasTarget)
                info += "\nMoving to waypoint";
            else
                info += "\nSearching for waypoint";
            UnityEditor.Handles.Label(position + Vector3.up * 4f, info);
        }
    }
}
#endif
