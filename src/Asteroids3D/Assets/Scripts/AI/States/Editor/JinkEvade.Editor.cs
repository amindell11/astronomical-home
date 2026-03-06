#if UNITY_EDITOR
using Game;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class JinkEvade
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);
            if (ctx==null) return;

            var selfPos = ctx.ShipInfo.Pos3D;
            var tgtPos  = GamePlane.PlanePointToWorld(currentTarget);

            // Draw path line
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(selfPos, tgtPos);
            Gizmos.DrawWireSphere(tgtPos, 1.2f);

            // Draw flee + jink vectors
            if (ctx.Combat.HasEnemy)
            {
                var enemyPos = GamePlane.PlanePointToWorld(ctx.Combat.EnemyPos);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(selfPos, enemyPos);
            }

            // Label
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, $"JINK EVADE\nNext flip in {(nextJinkTime - Time.time):F1}s");
        }
    }
}
#endif
