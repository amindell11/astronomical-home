#if UNITY_EDITOR
using Game;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Kite
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);
            
            if (!ctx) return;
            
            Vector3 selfPos = ctx.SelfPosition3D;
            
            // Draw kite radius circle
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f); // Cyan
            if (ctx.Enemy)
            {
                Vector3 enemyPos = GamePlane.PlanePointToWorld(ctx.EnemyPos);
                Gizmos.DrawWireSphere(enemyPos, utilityTuning.kiteDesiredDistance);
                
                // Draw min/max kite range
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Yellow
                Gizmos.DrawWireSphere(enemyPos, utilityTuning.kiteMinDistance);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Orange  
                Gizmos.DrawWireSphere(enemyPos, utilityTuning.kiteMaxDistance);
                
                // Draw arrowhead for retreat direction
                Vector2 dirAway = (ctx.SelfPosition - ctx.EnemyPos).normalized;
                Vector2 dirOppVel = ctx.EnemyVel.sqrMagnitude > 0.01f ? (-ctx.EnemyVel).normalized : Vector2.zero;
                Vector2 retreatDir = (dirAway + dirOppVel).normalized;
                if (retreatDir.sqrMagnitude < 0.01f) retreatDir = dirAway; // fallback

                Vector3 retreatDir3D = GamePlane.PlaneDirToWorld(retreatDir);
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(selfPos, retreatDir3D * 5f);
                
                // Draw arrowhead for orbit direction
                Vector3 perpLeft = Vector3.Cross(retreatDir3D, Vector3.forward).normalized;
                Vector3 arrowTip = selfPos + retreatDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - retreatDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - retreatDir3D * 1f - perpLeft * 0.5f);
                
                // Line to enemy
                float distToEnemy = ctx.VectorToEnemy.magnitude;
                Gizmos.color = ctx.LineOfSightToEnemy ? Color.green : Color.red;
                Gizmos.DrawLine(selfPos, enemyPos);
                
                // Show kite state info
                UnityEditor.Handles.color = Color.white;
                string info = $"KITE (Retreat)\n";
                info += $"Range: {distToEnemy:F1}m (target: {utilityTuning.kiteDesiredDistance:F0}m)\n";
                info += $"HP: {ctx.HealthPct:P0} Shield: {ctx.ShieldPct:P0}\n";
                if (ctx.LineOfSightToEnemy) info += "✓ Clear shot";
                else info += "✗ No LOS";
                
                UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, info);
            }
        }
    }
}
#endif
