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

        public override StateType Type => Profile.stateType;

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

        public AIState(StateProfile profile, Navigator navigator, Gunner gunner, UtilityTuning utilityTuning)
            : base(navigator, gunner, utilityTuning)
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
            // Configure navigator for this state
            switch (Profile.goalMode)
            {
                case GoalMode.MaintainRange:
                    navigator.SetGoalMaintainRange(Profile.desiredRange, Profile.rangeTolerance);
                    break;
                case GoalMode.Flee:
                    navigator.SetGoalFlee();
                    break;
                default:
                    navigator.ClearGoalMode();
                    break;
            }

            navigator.SetWeightMultipliers(Profile.weightMultipliers);

            if (!Profile.enableFiring)
                gunner.SetTarget((Transform)null);

            if (Profile.goalStrategy == GoalStrategy.RandomWaypoint && ctx != null)
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
            navigator.ClearGoalMode();
            navigator.ClearEnemyState();
            navigator.ClearObstacleExclusion();
            navigator.ClearWeightMultipliers();

            hasPatrolTarget = false;
            LastIntent = NavigationIntent.None;
        }

        private NavigationIntent ProduceIntent(Info ctx, float deltaTime)
        {
            var intent = new NavigationIntent
            {
                isValid = true,
                goalMode = Profile.goalMode,
                desiredRange = Profile.desiredRange,
                rangeTolerance = Profile.rangeTolerance,
                enableFiring = Profile.enableFiring,
                weightMultipliers = Profile.weightMultipliers,
            };

            switch (Profile.goalStrategy)
            {
                case GoalStrategy.RandomWaypoint:
                    TickPatrol(ctx, deltaTime, ref intent);
                    break;

                case GoalStrategy.TrackEnemy:
                    TickTrackEnemy(ctx, ref intent);
                    break;

                case GoalStrategy.FleeEnemy:
                    TickFleeEnemy(ctx, ref intent);
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
        }

        private void TickPatrol(Info ctx, float deltaTime, ref NavigationIntent intent)
        {
            if (!navigator.CurrentWaypoint.isValid ||
                ctx.Nav.VectorToWaypoint.magnitude < Profile.patrolArriveRadius)
            {
                ChooseNewPatrolPoint(ctx);
            }
            else
            {
                var dist = ctx.Nav.VectorToWaypoint.magnitude;
                if (dist < bestDistToWaypoint - Profile.patrolStuckProgressThreshold)
                {
                    bestDistToWaypoint = dist;
                    stuckTimer = 0f;
                }
                else
                {
                    stuckTimer += deltaTime;
                    if (stuckTimer >= Profile.patrolStuckTimeout)
                        ChooseNewPatrolPoint(ctx);
                }
            }

            intent.goalPosition = patrolTarget;
        }

        private void ChooseNewPatrolPoint(Info ctx)
        {
            var currentPos = ctx.ShipInfo.Pos;
            var randomDistance = Random.Range(
                Profile.patrolRadius * Profile.patrolMinDistanceFactor,
                Profile.patrolRadius);
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
            if (Profile.goalStrategy != GoalStrategy.RandomWaypoint)
            {
                navigator.SetNavigationPoint(
                    intent.goalPosition,
                    true,
                    intent.goalVelocity);
            }

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
            var a = ctx.Assessment;
            var f = Profile.utilityFactors;

            // Patrol: simple binary factor
            if (Profile.goalStrategy == GoalStrategy.RandomWaypoint)
            {
                return NewBuilder()
                    .FactorBinary(!a.InCombat, "noCombat", f.noCombatFactor)
                    .Build();
            }

            // Pursuit: distance-based
            if (Profile.stateType == StateType.Pursuit)
            {
                var distNorm = Mathf.Clamp01(a.EnemyDistance / f.engageDistance);
                return NewBuilder()
                    .Factor("selfHealth", a.HealthPct, f.healthFactor)
                    .Factor("selfShield", a.ShieldPct, f.shieldFactor)
                    .Factor("distance", distNorm, f.distanceFactor)
                    .Factor("threat", a.Outnumbered, f.threatFactor)
                    .Build();
            }

            // Evade: lots of conditional factors
            if (Profile.stateType == StateType.Evade)
            {
                var fightingRetreat = a.HealthPct < f.fightingRetreatHealthThreshold
                    && a.ShieldPct > f.fightingRetreatShieldThreshold;

                return NewBuilder()
                    .Factor("selfHealth", a.HealthPct, f.healthFactor)
                    .Factor("selfShield", a.ShieldPct, f.shieldFactor)
                    .Factor("outnumbered", a.Outnumbered, f.outnumberedFactor)
                    .FactorBinary(a.HasLineOfSight, "enemyLOS", f.enemyLOSFactor)
                    .Factor("closing", a.ClosingRate, f.closingSpeedFactor)
                    .Factor("enemyFacing", a.EnemyFacingThreat, f.enemyFacingFactor)
                    .FactorIf(a.IncomingMissile, "missile", f.missileFactor)
                    .FactorIf(a.EnemyDistance < f.tooCloseDistance && a.HasLineOfSight, "tooClose", f.tooCloseFactor)
                    .FactorIf(fightingRetreat, "fightingRetreat", f.fightingRetreatFactor)
                    .FactorIf(a.IncomingMissile, "missilePenalty", f.missilePenaltyFactor)
                    .Factor("angle", a.SelfAngleNorm, f.angleFactor)
                    .Build();
            }

            // Combat states (Attack, AttackAggressive, AttackEvasive)
            var inRange = a.EnemyDistance >= f.optimalRangeMin && a.EnemyDistance <= f.optimalRangeMax;
            float rangeScore;

            if (f.rangeScoreSpan > 0f)
                rangeScore = inRange ? 1f : Mathf.Clamp01(1f - Mathf.Abs(a.EnemyDistance - f.rangeScoreCenter) / f.rangeScoreSpan);
            else
                rangeScore = inRange ? 1f : 0.3f;

            var builder = NewBuilder()
                .Factor("selfHealth", a.HealthPct, f.healthFactor)
                .Factor("selfShield", a.ShieldPct, f.shieldFactor);

            // Optional factors — only add if non-neutral
            if (!IsInactive(f.enemyWeakFactor))
                builder.Factor("enemyWeak", a.EnemyCombinedDurability, f.enemyWeakFactor);

            if (!IsInactive(f.enemyFacingFactor))
                builder.Factor("enemyFacing", a.EnemyFacingThreat, f.enemyFacingFactor);

            if (!IsInactive(f.selfAngleFactor))
                builder.Factor("selfAngle", a.SelfAngleNorm, f.selfAngleFactor);

            if (!IsInactive(f.closingRateFactor))
                builder.Factor("closingRate", a.ClosingRate, f.closingRateFactor);

            builder.Factor("range", rangeScore, f.rangeFactor);
            builder.FactorBinary(a.HasLineOfSight, "LOS", f.losFactor);

            if (!IsInactive(f.threatFactor))
                builder.Factor("threat", a.Outnumbered, f.threatFactor);

            if (f.outerDistanceThreshold > 0f && a.EnemyDistance > f.outerDistanceThreshold)
                builder.FactorIf(true, "outerRange", f.outerRangeFactor);

            if (!IsInactive(f.desperationFactor))
                builder.Factor("desperation", a.HealthPct, f.desperationFactor);

            return builder.Build();
        }

        /// <summary>
        /// A factor is inactive if it's zeroed (unset struct default) or neutral (1,1).
        /// </summary>
        private static bool IsInactive(FactorRange range) =>
            (range.atLow == 0f && range.atHigh == 0f) ||
            (Mathf.Approximately(range.atLow, 1f) && Mathf.Approximately(range.atHigh, 1f));
    }
}
