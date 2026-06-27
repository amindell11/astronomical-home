using System.Linq;
using AI.Context;
using AI.Utility;
using Movement;
using Movement.MPC;
using UnityEngine;

namespace AI.States
{
    /// <summary>
    /// Unified state implementation driven by a StateProfile asset.
    /// Replaces all concrete state classes (Attack, Patrol, Evade, etc.)
    /// with a single data-driven class.
    /// </summary>
    public partial class AIState
    {
        protected readonly Navigator navigator;
        protected readonly Gunner gunner;

        public StateProfile Profile { get; }

        public string ProfileName => Profile.name;

        public UtilityBuilder LastBuilder { get; private set; }

        /// <summary>
        /// The NavigationIntent produced by the most recent Tick.
        /// Read by AICommander to apply to Navigator and Gunner.
        /// </summary>
        public NavigationIntent LastIntent { get; private set; }

        // Patrol state
        private Vector2 patrolTarget;
        private bool hasPatrolTarget;
        private float stuckTimer;
        private float bestDistToWaypoint;

        public AIState(StateProfile profile, Navigator navigator, Gunner gunner)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            Profile = profile;
        }

        public bool IsAvailable(AIContext ctx)
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

        public void Enter(AIContext ctx)
        {
            // Goal mode, weights, and enemy state are all (re)applied every Tick
            // through Navigator.ApplyIntent. Enter only does one-shot setup Tick can't redo.
            if (!Profile.enableFiring && gunner)
                gunner.SetTarget((Transform)null);

            if (Profile.goal is RandomWaypointGoal && ctx != null)
                ChooseNewPatrolPoint(ctx);
        }

        public void Tick(AIContext ctx, float deltaTime)
        {
            LastIntent = ProduceIntent(ctx, deltaTime);
            navigator.ApplyIntent(LastIntent);
            ApplyGunnerIntent(LastIntent);
        }

        public void Exit()
        {
            navigator.ResetNavigation();
            hasPatrolTarget = false;
            LastIntent = NavigationIntent.None;
        }

        private NavigationIntent ProduceIntent(AIContext ctx, float deltaTime)
        {
            var goal = Profile.goal;
            var intent = new NavigationIntent
            {
                isValid = true,
                goalMode = goal?.GoalMode ?? GoalMode.Waypoint,
                enableFiring = Profile.enableFiring,
                weightOverrides = Profile.weightOverrides,
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

        private void TickTrackEnemy(AIContext ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            intent.goalPosition = combat.EnemyPos;
            intent.goalVelocity = combat.EnemyVel;
            intent.obstacleExclusion = combat.Enemy.transform;

            if (Profile.enableTacticalCosts)
                SetEnemyTactical(ctx, ref intent);

            if (Profile.enableFiring)
                SetGunnerTarget(ctx, ref intent);
        }

        private void TickFleeEnemy(AIContext ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            if (!combat.HasEnemy) return;

            intent.goalPosition = combat.EnemyPos;
            intent.goalVelocity = combat.EnemyVel;
            intent.obstacleExclusion = combat.Enemy.transform;

            if (Profile.enableTacticalCosts)
                SetEnemyTactical(ctx, ref intent);

        }

        private void TickPatrol(AIContext ctx, float deltaTime, ref NavigationIntent intent)
        {
            var patrol = (RandomWaypointGoal)Profile.goal;

            if (!navigator.CurrentWaypoint.isValid ||
                VectorToWaypoint(ctx).magnitude < patrol.arriveRadius)
            {
                ChooseNewPatrolPoint(ctx);
            }
            else
            {
                var dist = VectorToWaypoint(ctx).magnitude;
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

        private void ChooseNewPatrolPoint(AIContext ctx)
        {
            var patrol = (RandomWaypointGoal)Profile.goal;
            var currentPos = ctx.Self.Pos;
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

        private void SetEnemyTactical(AIContext ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            var enemyFwd = combat.EnemyForward;
            intent.hasEnemy = true;
            intent.enemyYawDeg = Mathf.Atan2(-enemyFwd.x, enemyFwd.y) * Mathf.Rad2Deg;
            intent.enemyYawRateDeg = combat.EnemyYawRate;
            intent.projectileSpeed = ProjectileSpeed;
            intent.enemyDynamics = combat.EnemyDynamics;
        }

        /// <summary>Speed of the primary weapon's projectile, used for intercept lead. 0 when unarmed.</summary>
        private float ProjectileSpeed => gunner ? gunner.PrimaryProjectileSpeed : 0f;

        /// <summary>Plane-space vector from the ship to the navigator's current waypoint,
        /// or zero when there is no valid waypoint.</summary>
        private Vector2 VectorToWaypoint(AIContext ctx) =>
            navigator?.CurrentWaypoint.isValid == true
                ? navigator.CurrentWaypoint.position - ctx.Self.Pos
                : Vector2.zero;

        private void SetGunnerTarget(AIContext ctx, ref NavigationIntent intent)
        {
            var combat = ctx.Combat;
            intent.gunnerTarget = ctx.TargetingUtils.PredictIntercept(
                combat.EnemyPos, combat.EnemyVel, ProjectileSpeed);
        }

        /// <summary>
        /// Gunner is the one actuator the Navigator doesn't own, so its slice of the intent
        /// is applied here. All navigation flows through <see cref="Navigator.ApplyIntent"/>.
        /// </summary>
        private void ApplyGunnerIntent(in NavigationIntent intent)
        {
            if (!intent.isValid || !gunner) return;
            if (intent.enableFiring && intent.gunnerTarget.sqrMagnitude > 0f)
                gunner.SetTarget(intent.gunnerTarget);
        }

        // ── Utility Scoring ──

        public float ComputeUtility(AIContext ctx)
        {
            var builder = NewBuilder();
            if (Profile.utilityFactors == null) return builder.Build();
            foreach (var factor in Profile.utilityFactors.Where(factor => factor != null)) {
                builder.Factor(factor.Name, factor.Evaluate(ctx.Assessment), factor.weight);
            }
            return builder.Build();
        }

        private UtilityBuilder NewBuilder()
        {
            LastBuilder = new UtilityBuilder();
            return LastBuilder;
        }
    }
}
