#if UNITY_EDITOR
using Game;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Attack
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);
            
            Vector3 position = ctx.SelfPosition3D;
                    
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(position, utilityTuning.attackFacingDistance); // Close range
            
            // Draw target information if we have an enemy
            if (ctx.Enemy)
            {
                Vector3 enemyPos = GamePlane.PlanePointToWorld(ctx.EnemyPos);
                float distToEnemy = ctx.VectorToEnemy.magnitude;
                
                // Line to enemy - color based on line of sight
                Gizmos.color = ctx.LineOfSightToEnemy ? Color.red : new Color(1f, 0.5f, 0f, 0.7f);
                Gizmos.DrawLine(position, enemyPos);
                
                // Enemy marker
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(enemyPos, Vector3.one * 2f);
                
                // Show targeting crosshair if close
                if (distToEnemy < utilityTuning.attackFacingDistance)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(enemyPos, 1f);
                    
                    // Draw crosshair
                    Vector3 cross = Vector3.one * 0.5f;
                    Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                    cross.x *= -1;
                    Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                }
                
                // Enemy info label
                UnityEditor.Handles.color = Color.red;
                string enemyInfo = $"TARGET: {ctx.Enemy.name}\n{distToEnemy:F1}m";
                if (ctx.LineOfSightToEnemy) enemyInfo += " (LOS)";
                else enemyInfo += " (No LOS)";
                enemyInfo += $"\nEnemy HP: {ctx.EnemyHealthPct:P0}";
                UnityEditor.Handles.Label(enemyPos + Vector3.up, enemyInfo);
            }
            
            // Show attack state info
            UnityEditor.Handles.color = Color.white;
            string info = $"ATTACK\nHP: {ctx.HealthPct:P0} Shield: {ctx.ShieldPct:P0}";
            if (ctx.NearbyEnemyCount > ctx.NearbyFriendCount)
                info += $"\n⚠ Outnumbered {ctx.NearbyEnemyCount}v{ctx.NearbyFriendCount}";
            UnityEditor.Handles.Label(position + Vector3.up * 4f, info);
        }
    }
}
#endif
