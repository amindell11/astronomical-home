using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct EvadeTuning
    {
        [Header("Utility Factors")]
        [Tooltip("Utility weight based on self health. Low HP = more eager to evade.")]
        public FactorRange healthFactor;
        [Tooltip("Utility weight based on self shield. Low shield = more eager to evade.")]
        public FactorRange shieldFactor;
        [Tooltip("Utility boost when outnumbered by nearby enemies.")]
        public FactorRange outnumberedFactor;
        [Tooltip("Utility weight based on whether the enemy has line-of-sight.")]
        public FactorRange enemyLOSFactor;
        [Tooltip("Utility weight based on how fast the enemy is closing distance.")]
        public FactorRange closingSpeedFactor;
        [Tooltip("Utility weight based on how directly the enemy is aiming at us.")]
        public FactorRange enemyFacingFactor;
        [Tooltip("Flat utility boost when an incoming missile is detected.")]
        public float missileFactor;
        [Tooltip("Flat utility boost when enemy is within tooCloseDistance and has LOS.")]
        public float tooCloseFactor;
        [Tooltip("Distance threshold for the tooClose utility factor.")]
        public float tooCloseDistance;
        [Tooltip("Utility weight based on angle between self heading and enemy direction.")]
        public FactorRange angleFactor;

        [Header("Behavior")]
        [Tooltip("Distance used to compute a fallback evade point when no enemy is tracked.")]
        public float fleeDistance;
        [Tooltip("Utility penalty applied when an incoming missile is detected (stacks with missileFactor).")]
        public float missilePenaltyFactor;
        [Tooltip("Health threshold below which fightingRetreat kicks in.")]
        public float fightingRetreatHealthThreshold;
        [Tooltip("Shield threshold above which fightingRetreat kicks in (low HP but still shielded).")]
        public float fightingRetreatShieldThreshold;
        [Tooltip("Utility boost for fighting retreat (low health, shields still up).")]
        public float fightingRetreatFactor;

        public static EvadeTuning Default => new EvadeTuning
        {
            healthFactor = new FactorRange(1.3f, 0.7f),
            shieldFactor = new FactorRange(1.2f, 0.8f),
            outnumberedFactor = new FactorRange(0.8f, 1.3f),
            enemyLOSFactor = new FactorRange(0.7f, 1.2f),
            closingSpeedFactor = new FactorRange(0.8f, 1.2f),
            enemyFacingFactor = new FactorRange(0.9f, 1.2f),
            missileFactor = 1.5f,
            tooCloseFactor = 1.3f,
            tooCloseDistance = 7f,
            angleFactor = new FactorRange(0.7f, 1.0f),
            fleeDistance = 30f,
            missilePenaltyFactor = 0.8f,
            fightingRetreatHealthThreshold = 0.5f,
            fightingRetreatShieldThreshold = 0.5f,
            fightingRetreatFactor = 1.25f,
        };
    }
}
