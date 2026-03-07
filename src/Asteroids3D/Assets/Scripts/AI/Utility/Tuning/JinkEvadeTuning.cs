using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct JinkEvadeTuning
    {
        [Header("Utility Factors")]
        // Cross-borrowed from Evade defaults
        public FactorRange healthFactor;
        public FactorRange shieldFactor;
        public FactorRange outnumberedFactor;
        public FactorRange enemyLOSFactor;
        public FactorRange closingSpeedFactor;
        public FactorRange enemyFacingFactor;
        public float missileThreatFactor;
        public float criticalHealthThreshold;
        public float criticalShieldThreshold;
        public float criticalStateFactor;
        public float facingAwayAngle;
        public float facingAwayFactor;
        public FactorRange angleFactor;

        [Header("Jink Behavior")]
        public float fleeDistance;
        public float sideStepDistance;
        public float interval;
        public float missileAmplitudeFactor;

        public static JinkEvadeTuning Default => new JinkEvadeTuning
        {
            // Defaults match evade's current defaults (cross-borrowed)
            healthFactor = new FactorRange(1.3f, 0.7f),
            shieldFactor = new FactorRange(1.2f, 0.8f),
            outnumberedFactor = new FactorRange(0.8f, 1.3f),
            enemyLOSFactor = new FactorRange(0.7f, 1.2f),
            closingSpeedFactor = new FactorRange(0.8f, 1.2f),
            enemyFacingFactor = new FactorRange(0.9f, 1.2f),
            missileThreatFactor = 1.7f,
            criticalHealthThreshold = 0.3f,
            criticalShieldThreshold = 0.1f,
            criticalStateFactor = 1.5f,
            facingAwayAngle = 120f,
            facingAwayFactor = 1.2f,
            angleFactor = new FactorRange(0.7f, 1.0f),
            fleeDistance = 40f,
            sideStepDistance = 12f,
            interval = 1.2f,
            missileAmplitudeFactor = 1.5f,
        };
    }
}
