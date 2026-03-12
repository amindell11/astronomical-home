#if UNITY_EDITOR
using AI.Utility;
using Movement.MPC;
using UnityEditor;
using UnityEngine;

namespace AI.States.Editor
{
    /// <summary>
    /// One-shot editor utility to generate StateProfile assets from existing tuning defaults.
    /// Run from: AI / Generate State Profiles menu.
    /// </summary>
    public static class StateProfileGenerator
    {
        private const string OutputDir = "Assets/Settings/AI/StateProfiles";

        [MenuItem("AI/Generate State Profiles")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(OutputDir))
            {
                AssetDatabase.CreateFolder("Assets/Settings/AI", "StateProfiles");
            }

            CreateAttack();
            CreateAttackAggressive();
            CreateAttackEvasive();
            CreatePursuit();
            CreateEvade();
            CreatePatrol();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[StateProfileGenerator] Created 6 StateProfile assets in {OutputDir}");
        }

        private static StateProfile Create(string name)
        {
            var profile = ScriptableObject.CreateInstance<StateProfile>();
            AssetDatabase.CreateAsset(profile, $"{OutputDir}/{name}.asset");
            return profile;
        }

        private static void CreateAttack()
        {
            var t = AttackTuning.Default;
            var p = Create("Attack");
            p.stateType = StateType.Attack;
            p.goalStrategy = GoalStrategy.TrackEnemy;
            p.goalMode = GoalMode.MaintainRange;
            p.desiredRange = (t.optimalRangeMin + t.optimalRangeMax) * 0.5f;
            p.rangeTolerance = (t.optimalRangeMax - t.optimalRangeMin) * 0.5f;
            p.enableTacticalCosts = true;
            p.enableFiring = true;
            p.weightMultipliers = WeightMultipliers.Default;
            p.requiresEnemy = true;
            p.maxRange = t.optimalRangeMax;
            p.utilityFactors = new UtilityFactors
            {
                healthFactor = t.healthFactor,
                shieldFactor = t.shieldFactor,
                enemyWeakFactor = t.enemyWeakFactor,
                rangeFactor = t.rangeFactor,
                losFactor = t.losFactor,
                threatFactor = t.threatFactor,
                desperationFactor = t.desperationFactor,
                optimalRangeMin = t.optimalRangeMin,
                optimalRangeMax = t.optimalRangeMax,
                outerDistanceThreshold = t.outerDistanceThreshold,
                outerRangeFactor = t.outerRangeFactor,
                rangeScoreCenter = 20f,
                rangeScoreSpan = 30f,
            };
            EditorUtility.SetDirty(p);
        }

        private static void CreateAttackAggressive()
        {
            var t = AttackAggressiveTuning.Default;
            var p = Create("AttackAggressive");
            p.stateType = StateType.AttackAggressive;
            p.goalStrategy = GoalStrategy.TrackEnemy;
            p.goalMode = GoalMode.MaintainRange;
            p.desiredRange = (t.optimalRangeMin + t.optimalRangeMax) * 0.5f;
            p.rangeTolerance = (t.optimalRangeMax - t.optimalRangeMin) * 0.5f;
            p.enableTacticalCosts = true;
            p.enableFiring = true;
            p.weightMultipliers = new WeightMultipliers
            {
                pos = 1f, vel = 1f, yaw = 1f, yawRate = 1f, effort = 1f,
                smoothnessThrust = 1f, smoothnessStrafe = 1f, smoothnessYaw = 1f, obstacle = 1f,
                facing = 2f, exposure = 0.5f, los = 1f, tangential = 0f,
                facingWidth = 0.3f,
            };
            p.requiresEnemy = true;
            p.maxRange = t.optimalRangeMax;
            p.utilityFactors = new UtilityFactors
            {
                healthFactor = t.healthFactor,
                shieldFactor = t.shieldFactor,
                enemyWeakFactor = t.enemyWeakFactor,
                enemyFacingFactor = t.enemyFacingFactor,
                selfAngleFactor = t.selfAngleFactor,
                rangeFactor = t.rangeFactor,
                losFactor = t.losFactor,
                optimalRangeMin = t.optimalRangeMin,
                optimalRangeMax = t.optimalRangeMax,
                rangeScoreCenter = 15f,
                rangeScoreSpan = 25f,
            };
            EditorUtility.SetDirty(p);
        }

        private static void CreateAttackEvasive()
        {
            var t = AttackEvasiveTuning.Default;
            var p = Create("AttackEvasive");
            p.stateType = StateType.AttackEvasive;
            p.goalStrategy = GoalStrategy.TrackEnemy;
            p.goalMode = GoalMode.MaintainRange;
            p.desiredRange = (t.optimalRangeMin + t.optimalRangeMax) * 0.5f;
            p.rangeTolerance = (t.optimalRangeMax - t.optimalRangeMin) * 0.5f;
            p.enableTacticalCosts = true;
            p.enableFiring = true;
            p.weightMultipliers = new WeightMultipliers
            {
                pos = 1f, vel = 1f, yaw = 1f, yawRate = 1f, effort = 1f,
                smoothnessThrust = 1f, smoothnessStrafe = 1f, smoothnessYaw = 1f, obstacle = 1f,
                facing = 1f, exposure = 3f, los = 1f, tangential = 1.5f,
                exposureWidth = 0.8f,
            };
            p.requiresEnemy = true;
            p.maxRange = t.optimalRangeMax;
            p.utilityFactors = new UtilityFactors
            {
                healthFactor = t.healthFactor,
                shieldFactor = t.shieldFactor,
                enemyFacingFactor = t.enemyFacingFactor,
                closingRateFactor = t.closingRateFactor,
                rangeFactor = t.rangeFactor,
                losFactor = t.losFactor,
                optimalRangeMin = t.optimalRangeMin,
                optimalRangeMax = t.optimalRangeMax,
                rangeScoreCenter = 28f,
                rangeScoreSpan = 35f,
            };
            EditorUtility.SetDirty(p);
        }

        private static void CreatePursuit()
        {
            var t = PursuitTuning.Default;
            var p = Create("Pursuit");
            p.stateType = StateType.Pursuit;
            p.goalStrategy = GoalStrategy.TrackEnemy;
            p.goalMode = GoalMode.Waypoint;
            p.enableTacticalCosts = false;
            p.enableFiring = false;
            p.weightMultipliers = new WeightMultipliers
            {
                pos = 1f, vel = 1f, yaw = 1f, yawRate = 1f, effort = 1f,
                smoothnessThrust = 1f, smoothnessStrafe = 1f, smoothnessYaw = 1f, obstacle = 1f,
                facing = 0f, exposure = 0f, los = 0f, tangential = 0f,
            };
            p.requiresEnemy = true;
            p.minRange = AttackTuning.Default.optimalRangeMax;
            p.utilityFactors = new UtilityFactors
            {
                healthFactor = t.healthFactor,
                shieldFactor = t.shieldFactor,
                distanceFactor = t.distanceFactor,
                threatFactor = t.threatFactor,
                engageDistance = t.engageDistance,
            };
            EditorUtility.SetDirty(p);
        }

        private static void CreateEvade()
        {
            var t = EvadeTuning.Default;
            var p = Create("Evade");
            p.stateType = StateType.Evade;
            p.goalStrategy = GoalStrategy.FleeEnemy;
            p.goalMode = GoalMode.Flee;
            p.enableTacticalCosts = false;
            p.enableFiring = false;
            p.weightMultipliers = new WeightMultipliers
            {
                pos = 1f, vel = 1f, yaw = 1f, yawRate = 1f, effort = 1f,
                smoothnessThrust = 1f, smoothnessStrafe = 1f, smoothnessYaw = 1f, obstacle = 1f,
                facing = 0f, exposure = 0f, los = 0f, tangential = 0f,
            };
            p.requiresEnemy = true;
            p.utilityFactors = new UtilityFactors
            {
                healthFactor = t.healthFactor,
                shieldFactor = t.shieldFactor,
                outnumberedFactor = t.outnumberedFactor,
                enemyLOSFactor = t.enemyLOSFactor,
                closingSpeedFactor = t.closingSpeedFactor,
                enemyFacingFactor = t.enemyFacingFactor,
                missileFactor = t.missileFactor,
                tooCloseFactor = t.tooCloseFactor,
                tooCloseDistance = t.tooCloseDistance,
                angleFactor = t.angleFactor,
                missilePenaltyFactor = t.missilePenaltyFactor,
                fightingRetreatHealthThreshold = t.fightingRetreatHealthThreshold,
                fightingRetreatShieldThreshold = t.fightingRetreatShieldThreshold,
                fightingRetreatFactor = t.fightingRetreatFactor,
            };
            EditorUtility.SetDirty(p);
        }

        private static void CreatePatrol()
        {
            var t = PatrolTuning.Default;
            var p = Create("Patrol");
            p.stateType = StateType.Patrol;
            p.goalStrategy = GoalStrategy.RandomWaypoint;
            p.goalMode = GoalMode.Waypoint;
            p.enableTacticalCosts = false;
            p.enableFiring = false;
            p.weightMultipliers = new WeightMultipliers
            {
                pos = 1f, vel = 1f, yaw = 1f, yawRate = 1f, effort = 1f,
                smoothnessThrust = 1f, smoothnessStrafe = 1f, smoothnessYaw = 1f, obstacle = 1f,
                facing = 0f, exposure = 0f, los = 0f, tangential = 0f,
            };
            p.requiresNoEnemy = true;
            p.patrolRadius = t.radius;
            p.patrolMinDistanceFactor = t.minDistanceFactor;
            p.patrolArriveRadius = t.arriveRadius;
            p.patrolStuckTimeout = t.stuckTimeout;
            p.patrolStuckProgressThreshold = t.stuckProgressThreshold;
            p.utilityFactors = new UtilityFactors
            {
                noCombatFactor = new FactorRange(0.01f, 2.0f),
            };
            EditorUtility.SetDirty(p);
        }
    }
}
#endif
