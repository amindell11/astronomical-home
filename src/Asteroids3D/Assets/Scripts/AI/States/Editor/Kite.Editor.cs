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

            if (ctx == null) return;

            var selfPos = ctx.ShipInfo.Pos3D;
            var t = utilityTuning.kite;

            var combat = ctx.Combat;
            if (combat.HasEnemy)
            {
                var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);

                // Kite radius circles
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
                Gizmos.DrawWireSphere(enemyPos, t.desiredDistance);
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                Gizmos.DrawWireSphere(enemyPos, t.minDistance);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(enemyPos, t.maxDistance);

                // Retreat direction arrow
                var dirAway = (ctx.ShipInfo.Pos - combat.EnemyPos).normalized;
                var dirOppVel = combat.EnemyVel.sqrMagnitude > 0.01f ? (-combat.EnemyVel).normalized : Vector2.zero;
                var retreatDir = (dirAway + dirOppVel).normalized;
                if (retreatDir.sqrMagnitude < 0.01f) retreatDir = dirAway;

                var retreatDir3D = GamePlane.PlaneDirToWorld(retreatDir);
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(selfPos, retreatDir3D * 5f);

                var perpLeft = Vector3.Cross(retreatDir3D, Vector3.forward).normalized;
                var arrowTip = selfPos + retreatDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - retreatDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - retreatDir3D * 1f - perpLeft * 0.5f);
            }
        }
    }
}
#endif
