using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct OrbitTuning
    {
        [Header("Utility Factors")]
        // Cross-borrowed from Attack defaults
        public FactorRange healthFactor;
        public FactorRange shieldFactor;
        public FactorRange enemyWeakFactor;
        public FactorRange rangeFactor;
        public FactorRange losFactor;
        public FactorRange threatFactor;
        public FactorRange inRangeFactor;
        public float flankingFactor;
        public float lowHealthThreshold;
        public float lowHealthFactor;

        [Header("Orbit Geometry")]
        public float radius;
        public float minRadius;
        public float maxRadius;
        public float leadTime;
        public float flipMinTime;
        public float flipChancePerSecond;

        public static OrbitTuning Default => new OrbitTuning
        {
            // Defaults match attack's current defaults (cross-borrowed)
            healthFactor = new FactorRange(0.3f, 1.0f),
            shieldFactor = new FactorRange(0.4f, 1.0f),
            enemyWeakFactor = new FactorRange(1.3f, 1.0f),
            rangeFactor = new FactorRange(0.6f, 1.2f),
            losFactor = new FactorRange(0.4f, 1.2f),
            threatFactor = new FactorRange(1.0f, 0.5f),
            inRangeFactor = new FactorRange(0.7f, 1.4f),
            flankingFactor = 1.3f,
            lowHealthThreshold = 0.25f,
            lowHealthFactor = 0.6f,
            radius = 15f,
            minRadius = 10f,
            maxRadius = 25f,
            leadTime = 2f,
            flipMinTime = 3f,
            flipChancePerSecond = 0.1f,
        };
    }
}
