using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct KiteTuning
    {
        [Header("Utility Factors")]
        // Cross-borrowed from Evade defaults
        public FactorRange healthFactor;
        public FactorRange shieldFactor;
        public FactorRange outnumberedFactor;
        public float tooCloseFactor;
        public float lowHealthThreshold;
        public float highShieldThreshold;
        public float lowHealthHighShieldFactor;
        public float angleTolerance;
        public FactorRange angleFactor;

        [Header("Distance")]
        public float desiredDistance;
        public float minDistance;
        public float maxDistance;
        public float pushAwayDistance;
        public float returnDistanceFactor;

        public static KiteTuning Default => new KiteTuning
        {
            // Defaults match evade's current defaults (cross-borrowed)
            healthFactor = new FactorRange(1.3f, 0.7f),
            shieldFactor = new FactorRange(1.2f, 0.8f),
            outnumberedFactor = new FactorRange(0.8f, 1.3f),
            tooCloseFactor = 1.3f,
            lowHealthThreshold = 0.4f,
            highShieldThreshold = 0.6f,
            lowHealthHighShieldFactor = 1.2f,
            angleTolerance = 30f,
            angleFactor = new FactorRange(0.7f, 1.0f),
            desiredDistance = 10f,
            minDistance = 5f,
            maxDistance = 25f,
            pushAwayDistance = 5f,
            returnDistanceFactor = 0.5f,
        };
    }
}
