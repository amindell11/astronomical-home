using AI.Context;
using Movement.MPC;
using UnityEngine;

namespace AI.States
{
    /// <summary>
    /// Runtime companion to a <see cref="GoalStrategy"/>. Owns the goal-specific
    /// "what motion" behavior and any mutable per-ship state — which cannot live on
    /// the shared <see cref="StateProfile"/> asset, since one profile is reused across
    /// many ships. One GoalRunner is created per <see cref="AIState"/>; the state
    /// delegates motion to it and keeps only selection (utility/availability) and
    /// modulation (tactical/firing/weights) for itself.
    /// </summary>
    internal abstract partial class GoalRunner
    {
        public static GoalRunner Create(GoalStrategy goal, Navigator navigator) => goal switch
        {
            TrackEnemyGoal track  => new TrackEnemyRunner(track),
            FleeEnemyGoal flee    => new FleeEnemyRunner(flee),
            RandomWaypointGoal wp => new PatrolRunner(wp, navigator),
            _                     => new NullGoalRunner(),
        };

        /// <summary>One-shot setup when the owning state becomes active.</summary>
        public virtual void Enter(AIContext ctx) { }

        /// <summary>Clears mutable state when the owning state is left.</summary>
        public virtual void Reset() { }

        /// <summary>Fills the navigation (motion) fields of the intent for this goal.</summary>
        public abstract void Fill(AIContext ctx, float deltaTime, ref NavigationIntent intent);
    }

    /// <summary>Fallback for an unset/unknown goal: produces no motion.</summary>
    internal sealed partial class NullGoalRunner : GoalRunner
    {
        public override void Fill(AIContext ctx, float deltaTime, ref NavigationIntent intent) { }
    }

    /// <summary>Shared base for goals defined relative to the current enemy.</summary>
    internal abstract partial class EnemyGoalRunner : GoalRunner
    {
        public override void Fill(AIContext ctx, float deltaTime, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            intent.goalPosition = combat.EnemyPos;
            intent.goalVelocity = combat.EnemyVel;
            // Obstacle exclusion is now driven by the navigator from intent.target.source
            // (whenever hasTarget), so the goal no longer carries the enemy transform.
        }
    }

    /// <summary>Track and engage the enemy at a desired range.</summary>
    internal sealed partial class TrackEnemyRunner : EnemyGoalRunner
    {
        private readonly TrackEnemyGoal goal;

        public TrackEnemyRunner(TrackEnemyGoal goal) => this.goal = goal;

        public override void Fill(AIContext ctx, float deltaTime, ref NavigationIntent intent)
        {
            intent.desiredRange = goal.desiredRange;
            intent.rangeTolerance = goal.rangeTolerance;
            base.Fill(ctx, deltaTime, ref intent);
        }
    }

    /// <summary>Flee from the enemy (motion mirrors tracking; goal mode inverts it).</summary>
    internal sealed partial class FleeEnemyRunner : EnemyGoalRunner
    {
        public FleeEnemyRunner(FleeEnemyGoal goal) { }
    }

    /// <summary>
    /// Wander to random waypoints within a radius, re-targeting on arrival or when
    /// progress stalls. Holds the per-ship patrol bookkeeping the shared goal asset can't.
    /// </summary>
    internal sealed partial class PatrolRunner : GoalRunner
    {
        private readonly RandomWaypointGoal goal;
        private readonly Navigator navigator;

        private Vector2 patrolTarget;
        private bool hasPatrolTarget;
        private float stuckTimer;
        private float bestDistToWaypoint;

        public PatrolRunner(RandomWaypointGoal goal, Navigator navigator)
        {
            this.goal = goal;
            this.navigator = navigator;
        }

        public override void Enter(AIContext ctx)
        {
            if (ctx != null) ChooseNewPatrolPoint(ctx);
        }

        public override void Reset() => hasPatrolTarget = false;

        public override void Fill(AIContext ctx, float deltaTime, ref NavigationIntent intent)
        {
            if (!navigator.CurrentWaypoint.isValid ||
                VectorToWaypoint(ctx).magnitude < goal.arriveRadius)
            {
                ChooseNewPatrolPoint(ctx);
            }
            else
            {
                var dist = VectorToWaypoint(ctx).magnitude;
                if (dist < bestDistToWaypoint - goal.stuckProgressThreshold)
                {
                    bestDistToWaypoint = dist;
                    stuckTimer = 0f;
                }
                else
                {
                    stuckTimer += deltaTime;
                    if (stuckTimer >= goal.stuckTimeout)
                        ChooseNewPatrolPoint(ctx);
                }
            }

            intent.goalPosition = patrolTarget;
        }

        private void ChooseNewPatrolPoint(AIContext ctx)
        {
            var currentPos = ctx.Self.Pos;
            var randomDistance = Random.Range(
                goal.patrolRadius * goal.minDistanceFactor,
                goal.patrolRadius);
            var randomDirection = Random.insideUnitCircle.normalized;
            patrolTarget = currentPos + randomDirection * randomDistance;

            hasPatrolTarget = true;
            stuckTimer = 0f;
            bestDistToWaypoint = float.MaxValue;

            navigator.SetNavigationPoint(patrolTarget, true);
        }

        /// <summary>Plane-space vector from the ship to the navigator's current waypoint,
        /// or zero when there is no valid waypoint.</summary>
        private Vector2 VectorToWaypoint(AIContext ctx) =>
            navigator?.CurrentWaypoint.isValid == true
                ? navigator.CurrentWaypoint.position - ctx.Self.Pos
                : Vector2.zero;
    }
}
