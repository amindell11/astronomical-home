#if UNITY_EDITOR
using Game;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    public partial class AIState
    {
        public override void OnDrawGizmos(Info ctx)
        {
            base.OnDrawGizmos(ctx);
            if (ctx == null) return;

            var goal = Profile.goal;
            if (goal is RandomWaypointGoal)
                DrawPatrolGizmos(ctx);
            else if (goal is FleeEnemyGoal)
                DrawEvadeGizmos(ctx);
            else if (goal is TrackEnemyGoal)
                DrawCombatGizmos(ctx);
        }

        private void DrawPatrolGizmos(Info ctx)
        {
            var patrol = (RandomWaypointGoal)Profile.goal;
            var position = ctx.ShipInfo.Pos3D;

            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(position, patrol.patrolRadius);

            if (hasPatrolTarget)
            {
                var currentTargetWorld = GamePlane.PlanePointToWorld(patrolTarget);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, currentTargetWorld);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTargetWorld, patrol.arriveRadius);
                Gizmos.DrawWireCube(currentTargetWorld, Vector3.one * 0.5f);

                var distToTarget = Vector2.Distance(ctx.ShipInfo.Pos, patrolTarget);
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(currentTargetWorld + Vector3.up, $"Patrol Target\n{distToTarget:F1}m");
            }

            UnityEditor.Handles.color = Color.white;
            var info = $"PATROL\nRadius: {patrol.patrolRadius:F0}m";
            info += hasPatrolTarget ? "\nMoving to waypoint" : "\nSearching for waypoint";
            UnityEditor.Handles.Label(position + Vector3.up * 4f, info);
        }

        private void DrawEvadeGizmos(Info ctx)
        {
            var selfPos = ctx.ShipInfo.Pos3D;
            var combat = ctx.Combat;

            if (combat.HasEnemy)
            {
                var enemyPos3D = GamePlane.PlanePointToWorld(combat.EnemyPos);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(selfPos, enemyPos3D);

                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
                Gizmos.DrawWireSphere(enemyPos3D, 1f);

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

            if (ctx.Assessment.IncomingMissile)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(selfPos, Vector3.one * 3f);
            }
        }

        private void DrawCombatGizmos(Info ctx)
        {
            var position = ctx.ShipInfo.Pos3D;
            var combat = ctx.Combat;

            if (!combat.HasEnemy) return;

            var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);

            // Draw range band from RangeBandFactor if present
            foreach (var factor in Profile.utilityFactors)
            {
                if (factor is RangeBandFactor rb)
                {
                    Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.15f);
                    Gizmos.DrawWireSphere(enemyPos, rb.optimalMin);
                    Gizmos.DrawWireSphere(enemyPos, rb.optimalMax);

                    var desired = (rb.optimalMin + rb.optimalMax) * 0.5f;
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                    Gizmos.DrawWireSphere(enemyPos, desired);
                    break;
                }
            }

            // Targeting crosshair
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(enemyPos, 1f);
            var cross = Vector3.one * 0.5f;
            Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
            cross.x *= -1;
            Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);

            var a = ctx.Assessment;
            UnityEditor.Handles.color = new Color(1f, 0.5f, 0f);
            UnityEditor.Handles.Label(enemyPos + Vector3.up * 2f,
                $"{Profile.name}: Range {a.EnemyDistance:F1}m");
        }
    }
}
#endif
