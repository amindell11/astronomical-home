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

            var selfPos = ctx.ShipInfo.Pos3D;
            var evadePos3D = GamePlane.PlanePointToWorld(evadePoint);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(selfPos, evadePos3D);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(evadePos3D, 1f);

            // Draw distance to evade point
            var distToEvadePoint = Vector2.Distance(ctx.ShipInfo.Pos, evadePoint);
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(evadePos3D + Vector3.up, $"Evade Point\n{distToEvadePoint:F1}m");

            // Draw flee distance circle
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(selfPos, utilityTuning.evade.fleeDistance);

            // Draw threat indicators
            var combat = ctx.Combat;
            if (combat.HasEnemy)
            {
                var enemyPos = new Vector3(combat.EnemyPos.x, combat.EnemyPos.y, selfPos.z);

                // Draw line to primary threat
                Gizmos.color = ctx.Assessment.HasLineOfSight ? Color.red : new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(selfPos, enemyPos);

                // Draw flee direction arrow
                var fleeDir = -(combat.EnemyPos - ctx.ShipInfo.Pos).normalized;
                var fleeDir3D = new Vector3(fleeDir.x, fleeDir.y, 0);

                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(selfPos, fleeDir3D * 5f);

                // Draw arrowhead for flee direction
                var perpLeft = Vector3.Cross(fleeDir3D, Vector3.forward).normalized;
                var arrowTip = selfPos + fleeDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f - perpLeft * 0.5f);
            }

            // Highlight if incoming missile
            var a = ctx.Assessment;
            if (a.IncomingMissile)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(selfPos, Vector3.one * 3f);
            }

            // Draw state info
            UnityEditor.Handles.color = Color.white;
            var threatInfo = $"EVADE\nHP: {a.HealthPct:P0} Shield: {a.ShieldPct:P0}";
            if (a.IncomingMissile) threatInfo += "\n⚠ MISSILE!";
            if (a.NearbyEnemyCount > a.NearbyFriendCount) threatInfo += $"\n⚠ Outnumbered {a.NearbyEnemyCount}v{a.NearbyFriendCount+1}";

            UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, threatInfo);
        }
    }
}
#endif
