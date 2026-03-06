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

            var selfPos = ctx.ShipInfo.Pos3D;
            var t = utilityTuning.orbit;

            var combat = ctx.Combat;
            if (combat.HasEnemy)
            {
                var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);

                // Orbit radius circles
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                Gizmos.DrawWireSphere(enemyPos, t.radius);
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                Gizmos.DrawWireSphere(enemyPos, t.minRadius);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(enemyPos, t.maxRadius);

                // Orbit direction indicator
                var toEnemy = (combat.EnemyPos - ctx.ShipInfo.Pos).normalized;
                var orbitDir = orbitClockwise
                    ? new Vector2(toEnemy.y, -toEnemy.x)
                    : new Vector2(-toEnemy.y, toEnemy.x);

                var orbitDir3D = GamePlane.PlaneDirToWorld(orbitDir);
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(selfPos, orbitDir3D * 5f);

                var perpLeft = Vector3.Cross(orbitDir3D, Vector3.forward).normalized;
                var arrowTip = selfPos + orbitDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - orbitDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - orbitDir3D * 1f - perpLeft * 0.5f);
            }
        }
    }
}
#endif
