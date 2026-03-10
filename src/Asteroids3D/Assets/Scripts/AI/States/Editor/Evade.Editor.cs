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
                // In Flee mode, the nav target is the enemy position;
                // the MPC cost function drives the ship away.
                var enemyPos3D = GamePlane.PlanePointToWorld(combat.EnemyPos);

                // Line to enemy (threat source)
                Gizmos.color = Color.red;
                Gizmos.DrawLine(selfPos, enemyPos3D);

                // Enemy marker
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
                Gizmos.DrawWireSphere(enemyPos3D, 1f);

                // Flee direction arrow (away from enemy)
                var fleeDir = -(combat.EnemyPos - ctx.ShipInfo.Pos).normalized;
                var fleeDir3D = new Vector3(fleeDir.x, fleeDir.y, 0);

                Gizmos.color = Color.green;
                Gizmos.DrawRay(selfPos, fleeDir3D * 5f);

                var perpLeft = Vector3.Cross(fleeDir3D, Vector3.forward).normalized;
                var arrowTip = selfPos + fleeDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f - perpLeft * 0.5f);

                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(selfPos + Vector3.up * 2f,
                    $"FLEE (dist: {ctx.Assessment.EnemyDistance:F1}m)");
            }
            else
            {
                // No enemy: fallback evade point
                var evadePos3D = GamePlane.PlanePointToWorld(evadePoint);

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(selfPos, evadePos3D);
                Gizmos.DrawWireSphere(evadePos3D, 1f);

                var distToEvadePoint = Vector2.Distance(ctx.ShipInfo.Pos, evadePoint);
                UnityEditor.Handles.color = Color.cyan;
                UnityEditor.Handles.Label(evadePos3D + Vector3.up, $"Evade Point\n{distToEvadePoint:F1}m");
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
