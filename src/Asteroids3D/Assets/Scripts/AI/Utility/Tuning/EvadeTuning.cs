using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct EvadeTuning
    {
        [Header("Utility Factors")]
        public FactorRange healthFactor;
        public FactorRange shieldFactor;
        public FactorRange outnumberedFactor;
        public FactorRange enemyLOSFactor;
        public FactorRange closingSpeedFactor;
        public FactorRange enemyFacingFactor;
        public float missileFactor;
        public float tooCloseFactor;
        public float tooCloseDistance;
        public FactorRange angleFactor;

        [Header("Behavior")]
        public float fleeDistance;
        public float missilePenaltyFactor;
        public float fightingRetreatHealthThreshold;
        public float fightingRetreatShieldThreshold;
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
