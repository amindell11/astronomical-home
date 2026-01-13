using UnityEngine;

namespace AI.Utility
{
    /// <summary>
    /// Centralized tuning parameters for AI behavior.
    /// Factor ranges define how conditions affect utility scores using geometric mean.
    /// Factors around 1.0 are neutral, below 1.0 suppress, above 1.0 amplify.
    /// </summary>
    [CreateAssetMenu(fileName = "UtilityTuning", menuName = "AI/Utility Tuning")]
    public class UtilityTuning : ScriptableObject
    {
        [Header("State Weights (Shared Baseline)")]
        [Tooltip("Base weight multipliers for all states. These compound with per-instance biases.")]
        public UtilityWeights utilityWeights;
        
        [Header("Attack Utility Factors")]
        [Tooltip("Factor range for self health (low health = 0.3x, full health = 1.0x)")]
        public FactorRange attackHealthFactor = new FactorRange(0.3f, 1.0f);
        
        [Tooltip("Factor range for self shields (low shields = 0.4x, full shields = 1.0x)")]
        public FactorRange attackShieldFactor = new FactorRange(0.4f, 1.0f);
        
        [Tooltip("Factor range for enemy weakness (healthy = 1.0x, weak = 1.3x)")]
        public FactorRange attackEnemyWeakFactor = new FactorRange(1.3f, 1.0f);
        
        [Tooltip("Minimum distance for optimal attack range")]
        public float attackUtilityOptimalRangeMin = 6f;
        
        [Tooltip("Maximum distance for optimal attack range")]
        public float attackUtilityOptimalRangeMax = 40f;
        
        [Tooltip("Factor when in optimal range vs out of range (out = 0.6x, in = 1.2x)")]
        public FactorRange attackRangeFactor = new FactorRange(0.6f, 1.2f);
        
        [Tooltip("Factor range for line of sight (no LOS = 0.4x suppressed, has LOS = 1.2x boosted)")]
        public FactorRange attackLOSFactor = new FactorRange(0.4f, 1.2f);
        
        [Tooltip("Factor when outnumbered (safe = 1.0x, outnumbered = 0.5x)")]
        public FactorRange attackThreatFactor = new FactorRange(1.0f, 0.5f);
        
        [Header("Attack State Behavior")]
        [Tooltip("How often to update navigation target while attacking (seconds)")]
        public float attackTargetUpdateInterval = 0.5f;
        
        [Tooltip("Distance at which ship will face the enemy directly")]
        public float attackFacingDistance = 6f;
        
        [Tooltip("Relative velocity threshold for facing the enemy")]
        public float attackFacingSpeed = -1f;
        
        [Tooltip("Distance threshold for outer range consideration")]
        public float attackOuterDistanceThreshold = 25f;
        
        [Tooltip("Factor boost for attacking from outer range")]
        public float attackOuterRangeFactor = 1.15f;
        
        [Tooltip("Factor range for desperation (full health = 1.0x, critical = 1.2x)")]
        public FactorRange attackDesperationFactor = new FactorRange(1.2f, 1.0f);

        [Header("Orbit State")]
        [Tooltip("Preferred orbit radius around enemy")]
        public float orbitRadius = 15f;
        
        [Tooltip("Minimum orbit radius")]
        public float orbitMinRadius = 10f;
        
        [Tooltip("Maximum orbit radius")]
        public float orbitMaxRadius = 25f;
        
        [Tooltip("Lead time for computing orbit waypoint")]
        public float orbitLeadTime = 2f;
        
        [Tooltip("Factor range for orbit range (out of range = 0.7x, in range = 1.4x)")]
        public FactorRange orbitInRangeFactor = new FactorRange(0.7f, 1.4f);
        
        [Tooltip("Factor for flanking without line of sight")]
        public float orbitFlankingFactor = 1.3f;
        
        [Tooltip("Combined health+shield threshold for penalty")]
        public float orbitLowHealthThreshold = 0.25f;
        
        [Tooltip("Factor when health is too low for orbiting")]
        public float orbitLowHealthFactor = 0.6f;
        
        [Tooltip("Minimum time before considering orbit direction flip")]
        public float orbitFlipMinTime = 3f;
        
        [Tooltip("Chance per second of flipping orbit direction")]
        public float orbitFlipChancePerSecond = 0.1f;

        [Header("Jink Evade State")]
        [Tooltip("Forward component away from enemy when jinking")]
        public float jinkFleeDistance = 40f;
        
        [Tooltip("Lateral jink amplitude")]
        public float jinkSideStepDistance = 12f;
        
        [Tooltip("Seconds between jink direction flips")]
        public float jinkInterval = 1.2f;
        
        [Tooltip("Multiply jink amplitude by this factor when missile threat is present")]
        public float jinkMissileAmplitudeFactor = 1.5f;
        
        [Tooltip("Factor multiplier when missile is incoming")]
        public float jinkMissileThreatFactor = 1.7f;
        
        [Tooltip("Health threshold for critical state")]
        public float jinkCriticalHealthThreshold = 0.3f;
        
        [Tooltip("Shield threshold for critical state")]
        public float jinkCriticalShieldThreshold = 0.1f;
        
        [Tooltip("Factor multiplier when in critical health/shield state")]
        public float jinkCriticalStateFactor = 1.5f;
        
        [Tooltip("Angle threshold for facing-away bonus (degrees)")]
        public float jinkFacingAwayAngle = 120f;
        
        [Tooltip("Factor multiplier for facing away from enemy")]
        public float jinkFacingAwayFactor = 1.2f;
        
        [Tooltip("Factor range for facing angle (facing = 0.7x, away = 1.0x)")]
        public FactorRange jinkAngleFactor = new FactorRange(0.7f, 1.0f);

        [Header("Evade Utility Factors")]
        [Tooltip("Factor range for self health (full = 0.4x, critical = 1.0x)")]
        public FactorRange evadeHealthFactor = new FactorRange(1.3f, 0.7f);
        
        [Tooltip("Factor range for self shields (full = 0.5x, depleted = 1.0x)")]
        public FactorRange evadeShieldFactor = new FactorRange(1.2f, 0.8f);
        
        [Tooltip("Factor range for outnumbered state (advantage = 0.8x, outnumbered = 1.3x)")]
        public FactorRange evadeOutnumberedFactor = new FactorRange(0.8f, 1.3f);
        
        [Tooltip("Factor range for enemy LOS on self (no LOS = 0.7x safer, has LOS = 1.2x threatened)")]
        public FactorRange evadeEnemyLOSFactor = new FactorRange(0.7f, 1.2f);
        
        [Tooltip("Factor range for closing speed (0 to max)")]
        public FactorRange evadeClosingSpeedFactor = new FactorRange(0.8f, 1.2f);
        
        [Tooltip("Factor range for enemy facing us (not facing = 0.9x, facing = 1.2x)")]
        public FactorRange evadeEnemyFacingFactor = new FactorRange(0.9f, 1.2f);
        
        [Tooltip("Factor when missile is incoming")]
        public float evadeMissileFactor = 1.5f;
        
        [Tooltip("Distance threshold for too close consideration")]
        public float evadeUtilityTooCloseDistance = 7f;
        
        [Tooltip("Factor when too close to enemy")]
        public float evadeTooCloseFactor = 1.3f;
        
        [Header("Evade State Behavior")]
        [Tooltip("Distance to flee from threats")]
        public float evadeFleeDistance = 30f;
        
        [Tooltip("Factor penalty when missile is incoming (prefer Jink)")]
        public float evadeMissilePenaltyFactor = 0.8f;
        
        [Tooltip("Health threshold for fighting retreat")]
        public float evadeFightingRetreatHealthThreshold = 0.5f;
        
        [Tooltip("Shield threshold for fighting retreat")]
        public float evadeFightingRetreatShieldThreshold = 0.5f;
        
        [Tooltip("Factor for fighting retreat condition")]
        public float evadeFightingRetreatFactor = 1.25f;
        
        [Tooltip("Factor range for facing angle (facing enemy = 0.7x, away = 1.0x)")]
        public FactorRange evadeAngleFactor = new FactorRange(0.7f, 1.0f);

        [Header("Kite State")]
        [Tooltip("Desired distance to maintain while kiting")]
        public float kiteDesiredDistance = 10f;
        
        [Tooltip("Minimum kite distance")]
        public float kiteMinDistance = 5f;
        
        [Tooltip("Maximum kite distance")]
        public float kiteMaxDistance = 25f;
        
        [Tooltip("Extra distance to push when too close")]
        public float kitePushAwayDistance = 5f;
        
        [Tooltip("Distance factor when returning from too far")]
        public float kiteReturnDistanceFactor = 0.5f;
        
        [Tooltip("Factor when too close to enemy")]
        public float kiteTooCloseFactor = 1.3f;
        
        [Tooltip("Health threshold for kite consideration")]
        public float kiteLowHealthThreshold = 0.4f;
        
        [Tooltip("Shield threshold for kite consideration")]
        public float kiteHighShieldThreshold = 0.6f;
        
        [Tooltip("Factor for low health + high shield combination")]
        public float kiteLowHealthHighShieldFactor = 1.2f;
        
        [Tooltip("Angle tolerance before applying penalty (degrees)")]
        public float kiteAngleTolerance = 30f;
        
        [Tooltip("Factor range for facing angle (bad angle = 0.7x, good = 1.0x)")]
        public FactorRange kiteAngleFactor = new FactorRange(0.7f, 1.0f);

        [Header("Patrol State")]
        [Tooltip("Radius for selecting random patrol waypoints")]
        public float patrolRadius = 50f;
        
        [Tooltip("Minimum patrol distance factor (multiplied by patrol radius)")]
        public float patrolMinDistanceFactor = 0.3f;
    }
}
