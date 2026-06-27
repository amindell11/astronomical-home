#if UNITY_EDITOR
using AI.Context;
using Game;
using UnityEngine;

namespace AI.States
{
    internal abstract partial class GoalRunner
    {
        public virtual void OnDrawGizmos(AIContext ctx, StateProfile profile) { }
    }

    internal sealed partial class PatrolRunner
    {
        public override void OnDrawGizmos(AIContext ctx, StateProfile profile)
        {
            var position = ctx.Self.Pos3D;

            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(position, goal.patrolRadius);

            if (hasPatrolTarget)
            {
                var currentTargetWorld = GamePlane.PlanePointToWorld(patrolTarget);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, currentTargetWorld);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTargetWorld, goal.arriveRadius);
                Gizmos.DrawWireCube(currentTargetWorld, Vector3.one * 0.5f);

                var distToTarget = Vector2.Distance(ctx.Self.Pos, patrolTarget);
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(currentTargetWorld + Vector3.up, $"Patrol Target\n{distToTarget:F1}m");
            }

            UnityEditor.Handles.color = Color.white;
            var info = $"PATROL\nRadius: {goal.patrolRadius:F0}m";
            info += hasPatrolTarget ? "\nMoving to waypoint" : "\nSearching for waypoint";
            UnityEditor.Handles.Label(position + Vector3.up * 4f, info);
        }
    }

    internal sealed partial class FleeEnemyRunner
    {
        public override void OnDrawGizmos(AIContext ctx, StateProfile profile)
        {
            var selfPos = ctx.Self.Pos3D;
            var combat = ctx.Combat;

            if (combat.HasEnemy)
            {
                var enemyPos3D = GamePlane.PlanePointToWorld(combat.EnemyPos);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(selfPos, enemyPos3D);

                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
                Gizmos.DrawWireSphere(enemyPos3D, 1f);

                var fleeDir = -(combat.EnemyPos - ctx.Self.Pos).normalized;
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
    }

    internal sealed partial class TrackEnemyRunner
    {
        public override void OnDrawGizmos(AIContext ctx, StateProfile profile)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);

            // Draw range band from RangeBandFactor if present
            foreach (var factor in profile.utilityFactors)
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
                $"{profile.name}: Range {a.EnemyDistance:F1}m");
        }
    }
}
#endif
