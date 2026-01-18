#if UNITY_EDITOR
using Game;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Orbit
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);
            
            if (ctx == null) return;
            
            var selfPos = ctx.SelfPosition3D;
            
            // Draw orbit radius circles
            if (ctx.Enemy)
            {
                var enemyPos = GamePlane.PlanePointToWorld(ctx.EnemyPos);
                
                // Draw desired orbit radius
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Magenta
                Gizmos.DrawWireSphere(enemyPos, utilityTuning.orbitRadius);
                
                // Draw min/max orbit range
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Yellow
                Gizmos.DrawWireSphere(enemyPos, utilityTuning.orbitMinRadius);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Orange
                Gizmos.DrawWireSphere(enemyPos, utilityTuning.orbitMaxRadius);
                
                // Line to enemy
                var distToEnemy = ctx.VectorToEnemy.magnitude;
                Gizmos.color = ctx.LineOfSightToEnemy ? Color.magenta : new Color(1f, 0f, 0.5f);
                Gizmos.DrawLine(selfPos, enemyPos);
                
                // Draw orbit direction indicator
                var toEnemy = ctx.VectorToEnemy.normalized;
                var orbitDir = orbitClockwise 
                    ? new Vector2(toEnemy.y, -toEnemy.x)  // 90° CW
                    : new Vector2(-toEnemy.y, toEnemy.x); // 90° CCW
                
                var orbitDir3D = GamePlane.PlaneDirToWorld(orbitDir);
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(selfPos, orbitDir3D * 5f);
                
                // Draw arrowhead for orbit direction
                var perpLeft = Vector3.Cross(orbitDir3D, Vector3.forward).normalized;
                var arrowTip = selfPos + orbitDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - orbitDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - orbitDir3D * 1f - perpLeft * 0.5f);
                
                // Show orbit state info
                UnityEditor.Handles.color = Color.white;
                var direction = orbitClockwise ? "CW" : "CCW";
                var info = $"ORBIT ({direction})\n";
                info += $"Range: {distToEnemy:F1}m (target: {utilityTuning.orbitRadius:F0}m)\n";
                info += $"HP: {ctx.HealthPct:P0} Shield: {ctx.ShieldPct:P0}\n";
                if (ctx.LineOfSightToEnemy) info += "✓ Clear shot";
                else info += "✗ No LOS";
                
                UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, info);
            }
        }
    }
}
#endif
