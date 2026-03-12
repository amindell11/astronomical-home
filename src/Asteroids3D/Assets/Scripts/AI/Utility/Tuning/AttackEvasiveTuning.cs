using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct AttackEvasiveTuning
    {
        [Header("Utility Factors")]
        [Tooltip("Health weight. Low HP = more likely to pick evasive combat.")]
        public FactorRange healthFactor;
        [Tooltip("Shield weight.")]
        public FactorRange shieldFactor;
        [Tooltip("Boost when enemy is facing us (high threat = evasive). Input: EnemyFacingThreat.")]
        public FactorRange enemyFacingFactor;
        [Tooltip("Closing rate factor. Enemy closing fast = more evasive.")]
        public FactorRange closingRateFactor;
        [Tooltip("In-range factor.")]
        public FactorRange rangeFactor;
        [Tooltip("Line-of-sight factor.")]
        public FactorRange losFactor;

        [Header("Range")]
        [Tooltip("Inner edge of wider MPC MaintainRange band.")]
        public float optimalRangeMin;
        [Tooltip("Outer edge of wider MPC MaintainRange band.")]
        public float optimalRangeMax;

        [Header("MPC Weight Overrides")]
        [Tooltip("Exposure weight override (high — actively avoid enemy's arc).")]
        public float wExposure;
        [Tooltip("Tangential velocity weight (rewards lateral movement).")]
        public float wTangential;
        [Tooltip("Exposure cone sharpness.")]
        public float exposureWidth;

        public static AttackEvasiveTuning Default => new AttackEvasiveTuning
        {
            healthFactor = new FactorRange(1.2f, 0.7f),      // Low HP = higher utility
            shieldFactor = new FactorRange(1.1f, 0.8f),
            enemyFacingFactor = new FactorRange(0.4f, 1.3f),  // High threat = high utility
            closingRateFactor = new FactorRange(0.6f, 1.2f),  // Enemy closing = more evasive
            rangeFactor = new FactorRange(0.5f, 1.1f),
            losFactor = new FactorRange(0.4f, 1.1f),
            optimalRangeMin = 12f,
            optimalRangeMax = 45f,
            wExposure = 3.0f,
            wTangential = 1.5f,
            exposureWidth = 0.8f,
        };
    }
}
