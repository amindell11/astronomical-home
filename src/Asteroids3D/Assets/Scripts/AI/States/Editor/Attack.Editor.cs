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

            var position = ctx.ShipInfo.Pos3D;
            var combat = ctx.Combat;

            if (combat.HasEnemy)
            {
                var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);

                // Draw MaintainRange band around enemy
                var t = utilityTuning.attack;
                var desiredRange = (t.optimalRangeMin + t.optimalRangeMax) * 0.5f;
                var tolerance = (t.optimalRangeMax - t.optimalRangeMin) * 0.5f;
                var innerRadius = desiredRange - tolerance;
                var outerRadius = desiredRange + tolerance;

                Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.15f);
                Gizmos.DrawWireSphere(enemyPos, innerRadius);
                Gizmos.DrawWireSphere(enemyPos, outerRadius);

                // Desired range ring
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(enemyPos, desiredRange);

                // Targeting crosshair
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(enemyPos, 1f);
                var cross = Vector3.one * 0.5f;
                Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                cross.x *= -1;
                Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);

                // Distance label
                var a = ctx.Assessment;
                UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);
                UnityEditor.Handles.Label(enemyPos + Vector3.up * 2f,
                    $"Range: {a.EnemyDistance:F1}m\nBand: {innerRadius:F0}-{outerRadius:F0}m");
            }
        }
    }
}
#endif
