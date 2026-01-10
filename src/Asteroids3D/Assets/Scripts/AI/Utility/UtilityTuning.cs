using UnityEngine;

namespace AI.Utility
{
    /// <summary>
    /// Centralized tuning parameters for AI behavior.
    /// This ScriptableObject replaces magic numbers scattered throughout AI state classes.
    /// </summary>
    [CreateAssetMenu(fileName = "UtilityTuning", menuName = "AI/Utility Tuning")]
    public class UtilityTuning : ScriptableObject
    {
        [Header("State Weights (Shared Baseline)")]
        [Tooltip("Base weight multipliers for all states. These compound with per-instance biases.")]
        public UtilityWeights utilityWeights;
        
        [Header("Attack State")]
        [Tooltip("How often to update navigation target while attacking (seconds)")]
        public float attackTargetUpdateInterval = 0.5f;
        
        [Tooltip("Distance at which ship will face the enemy directly")]
        public float attackFacingDistance = 6f;
        
        [Tooltip("Relative velocity threshold for facing the enemy")]
        public float attackFacingSpeed = -1f;
        
        [Tooltip("Distance threshold for outer range bonus")]
        public float attackOuterDistanceThreshold = 25f;
        
        [Tooltip("Utility bonus for attacking from outer range")]
        public float attackOuterDistanceBonus = 0.2f;
        
        [Tooltip("Utility multiplier for low health desperate attacks")]
        public float attackLowHealthFearMultiplier = 0.15f;
        
        [Tooltip("Enemy health threshold for finish-off bonus")]
        public float attackEnemyHealthThreshold = 0.3f;

        [Header("Orbit State")]
        [Tooltip("Preferred orbit radius around enemy")]
        public float orbitRadius = 15f;
        
        [Tooltip("Minimum orbit radius")]
        public float orbitMinRadius = 10f;
        
        [Tooltip("Maximum orbit radius")]
        public float orbitMaxRadius = 25f;
        
        [Tooltip("Lead time for computing orbit waypoint")]
        public float orbitLeadTime = 2f;
        
        [Tooltip("Utility bonus for being in optimal orbit range")]
        public float orbitRangeBonus = 0.4f;
        
        [Tooltip("Utility bonus for flanking without line of sight")]
        public float orbitNoLosBonus = 0.3f;
        
        [Tooltip("Health threshold below which orbit is penalized")]
        public float orbitLowHealthThreshold = 0.25f;
        
        [Tooltip("Utility penalty for low health while orbiting")]
        public float orbitLowHealthPenalty = 0.4f;
        
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
        
        [Tooltip("Utility bonus when missile threat is detected")]
        public float jinkMissileThreatBonus = 0.7f;
        
        [Tooltip("Health threshold for critical jinking")]
        public float jinkCriticalHealthThreshold = 0.3f;
        
        [Tooltip("Shield threshold for critical jinking")]
        public float jinkCriticalShieldThreshold = 0.1f;
        
        [Tooltip("Utility bonus when in critical health/shield state")]
        public float jinkCriticalStateBonus = 0.4f;
        
        [Tooltip("Angle threshold for facing-away bonus (degrees)")]
        public float jinkFacingAwayAngle = 120f;
        
        [Tooltip("Utility bonus for facing away from enemy")]
        public float jinkFacingAwayBonus = 0.2f;
        
        [Tooltip("Penalty multiplier for facing toward enemy while jinking")]
        public float jinkAnglePenaltyMultiplier = 0.4f;

        [Header("Evade State")]
        [Tooltip("Distance to flee from threats")]
        public float evadeFleeDistance = 30f;
        
        [Tooltip("Utility penalty when missile is incoming (favor Jink instead)")]
        public float evadeMissilePenalty = 0.2f;
        
        [Tooltip("Health threshold for fighting retreat bonus")]
        public float evadeFightingRetreatHealthThreshold = 0.5f;
        
        [Tooltip("Shield threshold for fighting retreat bonus")]
        public float evadeFightingRetreatShieldThreshold = 0.5f;
        
        [Tooltip("Utility bonus for fighting retreat (low health, high shields)")]
        public float evadeFightingRetreatBonus = 0.25f;
        
        [Tooltip("Penalty multiplier for facing toward enemy while evading")]
        public float evadeAnglePenaltyMultiplier = 0.3f;

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
        
        [Tooltip("Utility bonus when too close to enemy")]
        public float kiteTooCloseBonus = 0.3f;
        
        [Tooltip("Health threshold for kite bonus")]
        public float kiteLowHealthThreshold = 0.4f;
        
        [Tooltip("Shield threshold for kite bonus")]
        public float kiteHighShieldThreshold = 0.6f;
        
        [Tooltip("Utility bonus for low health + high shield combination")]
        public float kiteLowHealthBonus = 0.2f;
        
        [Tooltip("Angle tolerance before applying penalty (degrees)")]
        public float kiteAngleTolerance = 30f;
        
        [Tooltip("Penalty multiplier for not facing enemy while kiting")]
        public float kiteAnglePenaltyMultiplier = 0.3f;

        [Header("Patrol State")]
        [Tooltip("Radius for selecting random patrol waypoints")]
        public float patrolRadius = 50f;
        
        [Tooltip("Minimum patrol distance factor (multiplied by patrol radius)")]
        public float patrolMinDistanceFactor = 0.3f;
    }
}
