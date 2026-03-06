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

            // Draw orbit radius circles
            var combat = ctx.CombatTracker;
            if (combat.HasEnemy)
            {
                var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);

                // Draw desired orbit radius
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Magenta
                Gizmos.DrawWireSphere(enemyPos, t.radius);

                // Draw min/max orbit range
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Yellow
                Gizmos.DrawWireSphere(enemyPos, t.minRadius);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Orange
                Gizmos.DrawWireSphere(enemyPos, t.maxRadius);

                // Line to enemy
                var a = ctx.Assessment;
                Gizmos.color = a.HasLineOfSight ? Color.magenta : new Color(1f, 0f, 0.5f);
                Gizmos.DrawLine(selfPos, enemyPos);

                // Draw orbit direction indicator
                var toEnemy = (combat.EnemyPos - ctx.ShipInfo.Pos).normalized;
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
                info += $"Range: {a.EnemyDistance:F1}m (target: {t.radius:F0}m)\n";
                info += $"HP: {a.HealthPct:P0} Shield: {a.ShieldPct:P0}\n";
                if (a.HasLineOfSight) info += "✓ Clear shot";
                else info += "✗ No LOS";

                UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, info);
            }
        }
    }
}
#endif
