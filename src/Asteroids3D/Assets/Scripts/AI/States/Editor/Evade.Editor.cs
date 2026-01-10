#if UNITY_EDITOR
using Game;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class Evade
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);
            
            
            Vector3 selfPos = ctx.SelfPosition3D;

            Vector3 evadePos3D = GamePlane.PlanePointToWorld(evadePoint);
                
            Gizmos.color = Color.green;
            Gizmos.DrawLine(selfPos, evadePos3D);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(evadePos3D, 1f);
            
            // Draw distance to evade point
            float distToEvadePoint = Vector2.Distance(ctx.SelfPosition, evadePoint);
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(evadePos3D + Vector3.up, $"Evade Point\n{distToEvadePoint:F1}m");

            // Draw flee distance circle
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(selfPos, utilityTuning.evadeFleeDistance);

            // Draw threat indicators
            if (ctx.Enemy)
            {
                Vector3 enemyPos = new Vector3(ctx.EnemyPos.x, ctx.EnemyPos.y, selfPos.z);
                
                // Draw line to primary threat
                Gizmos.color = ctx.LineOfSightToEnemy ? Color.red : new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(selfPos, enemyPos);
                
                // Draw flee direction arrow
                Vector2 fleeDir = ctx.Enemy.gameObject.activeInHierarchy ? 
                    -ctx.VectorToEnemy.normalized : Random.insideUnitCircle.normalized;
                Vector3 fleeDir3D = new Vector3(fleeDir.x, fleeDir.y, 0);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(selfPos, fleeDir3D * 5f);
                
                // Draw arrowhead for flee direction
                Vector3 perpLeft = Vector3.Cross(fleeDir3D, Vector3.forward).normalized;
                Vector3 arrowTip = selfPos + fleeDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f - perpLeft * 0.5f);
            }

            // Highlight if incoming missile
            if (ctx.IncomingMissile)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(selfPos, Vector3.one * 3f);
            }

            // Draw state info
            UnityEditor.Handles.color = Color.white;
            string threatInfo = $"EVADE\nHP: {ctx.HealthPct:P0} Shield: {ctx.ShieldPct:P0}";
            if (ctx.IncomingMissile) threatInfo += "\n⚠ MISSILE!";
            if (ctx.NearbyEnemyCount > ctx.NearbyFriendCount) threatInfo += $"\n⚠ Outnumbered {ctx.NearbyEnemyCount}v{ctx.NearbyFriendCount}";
            
            UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, threatInfo);
        }
    }
}
#endif
