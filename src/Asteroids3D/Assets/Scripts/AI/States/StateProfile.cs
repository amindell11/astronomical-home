using System;
using AI.Utility;
using Movement.MPC;
using UnityEngine;

namespace AI.States
{
    /// <summary>
    /// Complete data profile for a single AI state. Bundles navigation style,
    /// utility scoring factors, and availability conditions into one asset.
    /// </summary>
    [CreateAssetMenu(fileName = "StateProfile", menuName = "AI/State Profile")]
    public class StateProfile : ScriptableObject
    {
        [Header("Identity")]
        public StateType stateType;

        // ── Navigation Style ──

        [Header("Goal")]
        public GoalStrategy goalStrategy;
        public GoalMode goalMode;
        public float desiredRange;
        public float rangeTolerance;

        [Header("Tactical")]
        [Tooltip("Enable tactical MPC costs (facing, exposure, LOS, tangential).")]
        public bool enableTacticalCosts;
        [Tooltip("Enable weapon firing in this state.")]
        public bool enableFiring;

        [Header("MPC Weight Multipliers")]
        [Tooltip("Per-state multipliers for MPC weights. 1 = use base, 0 = disable, 2 = double.")]
        public WeightMultipliers weightMultipliers;

        // ── Availability ──

        [Header("Availability")]
        [Tooltip("State requires an enemy to be available.")]
        public bool requiresEnemy;
        [Tooltip("State requires NO enemy (e.g. Patrol).")]
        public bool requiresNoEnemy;
        [Tooltip("Minimum distance to enemy for availability. 0 = no minimum.")]
        public float minRange;
        [Tooltip("Maximum distance to enemy for availability. 0 = no maximum.")]
        public float maxRange;

        // ── Patrol Config ──

        [Header("Patrol (RandomWaypoint only)")]
        public float patrolRadius = 50f;
        public float patrolMinDistanceFactor = 0.3f;
        public float patrolArriveRadius = 3f;
        public float patrolStuckTimeout = 3f;
        public float patrolStuckProgressThreshold = 1f;

        // ── Utility Scoring ──

        [Header("Utility Factors")]
        public UtilityFactors utilityFactors;

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var commander in FindObjectsByType<CombatAICommander>(
                         UnityEngine.FindObjectsSortMode.None))
                commander.RefreshStates();
        }
#endif
    }

    /// <summary>
    /// Unified utility factor configuration. Each factor is a FactorRange applied
    /// via geometric mean. Neutral factors (1.0, 1.0) have no effect and can be
    /// left at default for states that don't use them.
    /// </summary>
    [Serializable]
    public struct UtilityFactors
    {
        // Common combat factors
        public FactorRange healthFactor;
        public FactorRange shieldFactor;
        public FactorRange enemyWeakFactor;
        public FactorRange rangeFactor;
        public FactorRange losFactor;
        public FactorRange threatFactor;
        public FactorRange desperationFactor;

        // Positional awareness
        public FactorRange enemyFacingFactor;
        public FactorRange selfAngleFactor;
        public FactorRange closingRateFactor;

        // Evade-specific
        public FactorRange outnumberedFactor;
        public FactorRange enemyLOSFactor;
        public FactorRange closingSpeedFactor;
        public FactorRange angleFactor;

        // Pursuit-specific
        public FactorRange distanceFactor;
        public float engageDistance;

        // Evade conditional factors
        public float missileFactor;
        public float tooCloseFactor;
        public float tooCloseDistance;
        public float missilePenaltyFactor;
        public float fightingRetreatHealthThreshold;
        public float fightingRetreatShieldThreshold;
        public float fightingRetreatFactor;

        // Range scoring
        public float optimalRangeMin;
        public float optimalRangeMax;
        public float outerDistanceThreshold;
        public float outerRangeFactor;
        [Tooltip("Center point for range score falloff.")]
        public float rangeScoreCenter;
        [Tooltip("Span for range score falloff. 0 = binary (in-range or out-of-range penalty).")]
        public float rangeScoreSpan;

        // Patrol (binary factor)
        public FactorRange noCombatFactor;

    }
}
