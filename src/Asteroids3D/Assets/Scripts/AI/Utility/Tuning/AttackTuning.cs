using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct AttackTuning
    {
        [Header("Utility Factors")]
        public FactorRange healthFactor;
        public FactorRange shieldFactor;
        public FactorRange enemyWeakFactor;
        public FactorRange rangeFactor;
        public FactorRange losFactor;
        public FactorRange threatFactor;
        public FactorRange desperationFactor;

        [Header("Range")]
        public float optimalRangeMin;
        public float optimalRangeMax;
        public float outerDistanceThreshold;
        public float outerRangeFactor;

        [Header("Behavior")]
        public float facingDistance;
        public float facingSpeed;
        public float targetUpdateInterval;

        public static AttackTuning Default => new AttackTuning
        {
            healthFactor = new FactorRange(0.3f, 1.0f),
            shieldFactor = new FactorRange(0.4f, 1.0f),
            enemyWeakFactor = new FactorRange(1.3f, 1.0f),
            rangeFactor = new FactorRange(0.6f, 1.2f),
            losFactor = new FactorRange(0.4f, 1.2f),
            threatFactor = new FactorRange(1.0f, 0.5f),
            desperationFactor = new FactorRange(1.2f, 1.0f),
            optimalRangeMin = 6f,
            optimalRangeMax = 40f,
            outerDistanceThreshold = 25f,
            outerRangeFactor = 1.15f,
            facingDistance = 6f,
            facingSpeed = -1f,
            targetUpdateInterval = 0.5f,
        };
    }
}
