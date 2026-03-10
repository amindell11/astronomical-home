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
            var combat = ctx.Combat;

            if (combat.HasEnemy)
            {
                var enemyPos3D = GamePlane.PlanePointToWorld(combat.EnemyPos);

                // Draw line to threat
                Gizmos.color = Color.red;
                Gizmos.DrawLine(selfPos, enemyPos3D);

                // Draw flee direction arrow
                var fleeDir = -(combat.EnemyPos - ctx.ShipInfo.Pos).normalized;
                var fleeDir3D = new Vector3(fleeDir.x, fleeDir.y, 0);

                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(selfPos, fleeDir3D * 5f);

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
        }
    }
}
#endif
