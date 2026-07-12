using AI.Context;
using Game;
using UnityEditor;
using UnityEngine;

namespace AI.States
{
    /// <summary>Draws the active state's goal-runner debug viz; dispatched from
    /// <see cref="BrainGizmos"/> under the StateDetail channel.</summary>
    internal static class GoalRunnerGizmos
    {
        public static void Draw(AIState state, AIContext ctx)
        {
            if (ctx == null) return;
            switch (state.goalRunner)
            {
                case PatrolRunner patrol: DrawPatrol(patrol, ctx); break;
                case FleeEnemyRunner: DrawFlee(ctx); break;
                case TrackEnemyRunner: DrawTrack(ctx, state.Profile); break;
            }
        }

        private static void DrawPatrol(PatrolRunner runner, AIContext ctx)
        {
            var position = ctx.Self.Transform.position;

            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(position, runner.goal.patrolRadius);

            if (runner.hasPatrolTarget)
            {
                var currentTargetWorld = GamePlane.PlanePointToWorld(runner.patrolTarget);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, currentTargetWorld);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTargetWorld, runner.goal.arriveRadius);
                Gizmos.DrawWireCube(currentTargetWorld, Vector3.one * 0.5f);

                var distToTarget = Vector2.Distance(ctx.Self.Kinematics.pos, runner.patrolTarget);
                Handles.color = Color.green;
                Handles.Label(currentTargetWorld + Vector3.up, $"Patrol Target\n{distToTarget:F1}m");
            }

            Handles.color = Color.white;
            var info = $"PATROL\nRadius: {runner.goal.patrolRadius:F0}m";
            info += runner.hasPatrolTarget ? "\nMoving to waypoint" : "\nSearching for waypoint";
            Handles.Label(position + Vector3.up * 4f, info);
        }

        private static void DrawFlee(AIContext ctx)
        {
            var selfPos = ctx.Self.Transform.position;
            var combat = ctx.Combat;

            if (combat.HasEnemy)
            {
                var enemyPos3D = GamePlane.PlanePointToWorld(combat.EnemyPos);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(selfPos, enemyPos3D);

                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
                Gizmos.DrawWireSphere(enemyPos3D, 1f);

                var fleeDir = -(combat.EnemyPos - ctx.Self.Kinematics.pos).normalized;
                var fleeDir3D = new Vector3(fleeDir.x, fleeDir.y, 0);

                Gizmos.color = Color.green;
                Gizmos.DrawRay(selfPos, fleeDir3D * 5f);

                var perpLeft = Vector3.Cross(fleeDir3D, Vector3.forward).normalized;
                var arrowTip = selfPos + fleeDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f - perpLeft * 0.5f);

                Handles.color = Color.green;
                Handles.Label(selfPos + Vector3.up * 2f,
                    $"FLEE (dist: {ctx.Assessment.EnemyDistance:F1}m)");
            }

            if (ctx.Assessment.IncomingMissile)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(selfPos, Vector3.one * 3f);
            }
        }

        private static void DrawTrack(AIContext ctx, StateProfile profile)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            var enemyPos = GamePlane.PlanePointToWorld(combat.EnemyPos);

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

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(enemyPos, 1f);
            var cross = Vector3.one * 0.5f;
            Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
            cross.x *= -1;
            Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);

            var a = ctx.Assessment;
            Handles.color = new Color(1f, 0.5f, 0f);
            Handles.Label(enemyPos + Vector3.up * 2f,
                $"{profile.name}: Range {a.EnemyDistance:F1}m");
        }
    }
}
