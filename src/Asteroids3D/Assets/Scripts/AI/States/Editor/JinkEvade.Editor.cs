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
            if (!ctx) return;

            Vector3 selfPos = ctx.SelfPosition3D;
            Vector3 tgtPos  = GamePlane.PlanePointToWorld(currentTarget);

            // Draw path line
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(selfPos, tgtPos);
            Gizmos.DrawWireSphere(tgtPos, 1.2f);

            // Draw flee + jink vectors
            if (ctx.Enemy)
            {
                Vector3 enemyPos = GamePlane.PlanePointToWorld(ctx.EnemyPos);
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
