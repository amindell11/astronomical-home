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

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(position, utilityTuning.attack.facingDistance);

            var combat = ctx.Combat;
            if (combat.HasEnemy)
            {
                var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);
                var a = ctx.Assessment;

                // Show targeting crosshair if close
                if (a.EnemyDistance < utilityTuning.attack.facingDistance)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(enemyPos, 1f);

                    var cross = Vector3.one * 0.5f;
                    Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                    cross.x *= -1;
                    Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                }
            }
        }
    }
}
#endif
