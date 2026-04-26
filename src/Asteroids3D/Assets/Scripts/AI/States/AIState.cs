using AI.Planning;
using AI.Utility;
using Movement;
using Movement.MPC;
using UnityEngine;
using Info = AI.Context.Info;

namespace AI.States
{
    /// <summary>
    /// Unified state implementation driven by a StateProfile asset.
    /// Replaces all concrete state classes (Attack, Patrol, Evade, etc.)
    /// with a single data-driven class.
    /// </summary>
    public partial class AIState : State
    {
        public StateProfile Profile { get; }

        public override string ProfileName => Profile.name;

        /// <summary>
        /// The NavigationIntent produced by the most recent Tick.
        /// Read by CombatAICommander to apply to Navigator and Gunner.
        /// </summary>
        public NavigationIntent LastIntent { get; private set; }

        // Patrol state
        private Vector2 patrolTarget;
        private bool hasPatrolTarget;
        private float stuckTimer;
        private float bestDistToWaypoint;

        public AIState(StateProfile profile, Navigator navigator, Gunner gunner)
            : base(navigator, gunner)
        {
            this.Profile = profile;
        }

        public override bool IsAvailable(Info ctx)
        {
            if (Profile.requiresEnemy && !ctx.Combat.HasEnemy)
                return false;
            if (Profile.requiresNoEnemy && ctx.Combat.HasEnemy)
                return false;
            if (Profile.minRange > 0f && ctx.Combat.HasEnemy && ctx.Assessment.EnemyDistance <= Profile.minRange)
                return false;
            if (Profile.maxRange > 0f && ctx.Combat.HasEnemy && ctx.Assessment.EnemyDistance > Profile.maxRange)
                return false;
            return true;
        }

        public override void Enter(Info ctx)
        {
            var goal = Profile.goal;
            if (goal is TrackEnemyGoal track)
                navigator.SetGoalMaintainRange(track.desiredRange, track.rangeTolerance);
            else if (goal is FleeEnemyGoal)
                navigator.SetGoalFlee();
            else
                navigator.ClearGoalMode();

            navigator.SetWeightMultipliers(Profile.weightMultipliers);

            if (!Profile.enableFiring)
                gunner.SetTarget((Transform)null);

            if (goal is RandomWaypointGoal && ctx != null)
                ChooseNewPatrolPoint(ctx);
        }

        public override void Tick(Info ctx, float deltaTime)
        {
            LastIntent = ProduceIntent(ctx, deltaTime);
            ApplyIntent(LastIntent, ctx);
        }

        public override void Exit()
        {
            navigator.ClearNavigationPoint();
            navigator.ClearNavigationTarget();
            navigator.ClearGoalMode();
            navigator.ClearEnemyState();
            navigator.ClearObstacleExclusion();
            navigator.ClearWeightMultipliers();

            hasPatrolTarget = false;
            LastIntent = NavigationIntent.None;
        }

        private NavigationIntent ProduceIntent(Info ctx, float deltaTime)
        {
            var goal = Profile.goal;
            var intent = new NavigationIntent
            {
                isValid = true,
                goalMode = goal?.GoalMode ?? GoalMode.Waypoint,
                enableFiring = Profile.enableFiring,
                weightMultipliers = Profile.weightMultipliers,
            };

            switch (goal)
            {
                case TrackEnemyGoal track:
                    intent.desiredRange = track.desiredRange;
                    intent.rangeTolerance = track.rangeTolerance;
                    TickTrackEnemy(ctx, ref intent);
                    break;
                case FleeEnemyGoal:
                    TickFleeEnemy(ctx, ref intent);
                    break;
                case RandomWaypointGoal:
                    TickPatrol(ctx, deltaTime, ref intent);
                    break;
            }

            return intent;
        }

        private void TickTrackEnemy(Info ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            intent.goalPosition = combat.EnemyPos;
            intent.goalVelocity = combat.EnemyVel;
            intent.obstacleExclusion = combat.Enemy.transform;

            // Skip routing when close enough that MaintainRange owns the approach.
            var track = Profile.goal as TrackEnemyGoal;
            var distToEnemy = (ctx.ShipInfo.Pos - combat.EnemyPos).magnitude;
            var closeRange = track != null && distToEnemy < 1.5f * track.desiredRange;
            if (!closeRange)
                TrySetRoutingTarget(ctx, ref intent, RoutingMode.Chase);

            if (Profile.enableTacticalCosts)
                SetEnemyTactical(ctx, ref intent);

            if (Profile.enableFiring)
                SetGunnerTarget(ctx, ref intent);
        }

        private void TickFleeEnemy(Info ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            intent.goalPosition = combat.EnemyPos;
            intent.goalVelocity = combat.EnemyVel;
            intent.obstacleExclusion = combat.Enemy.transform;

            TrySetRoutingTarget(ctx, ref intent, RoutingMode.Evade);
        }

        private static void TrySetRoutingTarget(Info ctx, ref NavigationIntent intent, RoutingMode mode)
        {
            if (!ctx.NavPlanner) return;
            var enemy = ctx.Combat.Enemy;
            if (!enemy) return;
            if (!ctx.NavPlanner.TryGetRoutedPlanePos(ctx.ShipInfo.Pos, enemy, mode, out var routedPlane))
                return;
            intent.navigationTarget = routedPlane;
        }

        private void TickPatrol(Info ctx, float deltaTime, ref NavigationIntent intent)
        {
            var patrol = (RandomWaypointGoal)Profile.goal;

            if (!navigator.CurrentWaypoint.isValid ||
                ctx.Nav.VectorToWaypoint.magnitude < patrol.arriveRadius)
            {
                ChooseNewPatrolPoint(ctx);
            }
            else
            {
                var dist = ctx.Nav.VectorToWaypoint.magnitude;
                if (dist < bestDistToWaypoint - patrol.stuckProgressThreshold)
                {
                    bestDistToWaypoint = dist;
                    stuckTimer = 0f;
                }
                else
                {
                    stuckTimer += deltaTime;
                    if (stuckTimer >= patrol.stuckTimeout)
                        ChooseNewPatrolPoint(ctx);
                }
            }

            intent.goalPosition = patrolTarget;
        }

        private void ChooseNewPatrolPoint(Info ctx)
        {
            var patrol = (RandomWaypointGoal)Profile.goal;
            var currentPos = ctx.ShipInfo.Pos;
            var randomDistance = Random.Range(
                patrol.patrolRadius * patrol.minDistanceFactor,
                patrol.patrolRadius);
            var randomDirection = Random.insideUnitCircle.normalized;
            patrolTarget = currentPos + randomDirection * randomDistance;

            hasPatrolTarget = true;
            stuckTimer = 0f;
            bestDistToWaypoint = float.MaxValue;

            navigator.SetNavigationPoint(patrolTarget, true);
        }

        private static void SetEnemyTactical(Info ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            var enemyFwd = combat.EnemyForward;
            intent.hasEnemy = true;
            intent.enemyYawDeg = Mathf.Atan2(-enemyFwd.x, enemyFwd.y) * Mathf.Rad2Deg;
            intent.enemyYawRateDeg = combat.EnemyYawRate;
            intent.projectileSpeed = combat.LaserSpeed;
            intent.enemyDynamics = combat.EnemyDynamics;
        }

        private void SetGunnerTarget(Info ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            intent.gunnerTarget = ctx.TargetingUtils.PredictIntercept(
                combat.EnemyPos, combat.EnemyVel, combat.LaserSpeed);
        }

        /// <summary>
        /// Apply the intent to the navigator and gunner. This is the bridge
        /// between the declarative intent and the current imperative navigator API.
        /// Will be replaced by Navigator.ApplyIntent() in a future pass.
        /// </summary>
        private void ApplyIntent(NavigationIntent intent, Info ctx)
        {
            if (!intent.isValid) return;

            // Obstacle exclusion
            if (intent.obstacleExclusion)
                navigator.SetObstacleExclusion(intent.obstacleExclusion);

            // Navigation point (not for patrol — already set in ChooseNewPatrolPoint)
            if (!(Profile.goal is RandomWaypointGoal))
            {
                navigator.SetNavigationPoint(
                    intent.goalPosition,
                    true,
                    intent.goalVelocity);
            }

            // High-level planner routing override
            if (intent.navigationTarget.HasValue)
                navigator.SetNavigationTarget(intent.navigationTarget.Value);
            else
                navigator.ClearNavigationTarget();

            // Enemy state for MPC tactical costs (includes dynamics for physics-based rollout)
            if (intent.hasEnemy)
                navigator.SetEnemyState(intent.enemyYawDeg, intent.enemyYawRateDeg, intent.projectileSpeed,
                    intent.enemyDynamics);

            // Gunner target
            if (intent.enableFiring && intent.gunnerTarget.sqrMagnitude > 0f)
                gunner.SetTarget(intent.gunnerTarget);
        }

        // ── Utility Scoring ──

        public override float ComputeUtility(Info ctx)
        {
            var builder = NewBuilder();
            if (Profile.utilityFactors != null)
            {
                foreach (var factor in Profile.utilityFactors)
                {
                    if (factor != null)
                        builder.Factor(factor.Name, factor.Evaluate(ctx.Assessment), factor.weight);
                }
            }
            return builder.Build();
        }
    }
}
