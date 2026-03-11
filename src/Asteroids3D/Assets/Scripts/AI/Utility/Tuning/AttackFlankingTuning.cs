using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct AttackFlankingTuning
    {
        [Header("Utility Factors")]
        [Tooltip("Health weight.")]
        public FactorRange healthFactor;
        [Tooltip("Shield weight.")]
        public FactorRange shieldFactor;
        [Tooltip("Self-angle factor. Mid-range angle = flanking opportunity. Input: SelfAngleNorm.")]
        public FactorRange selfAngleFactor;
        [Tooltip("Enemy facing factor. Moderate threat = flanking opportunity. Input: EnemyFacingThreat.")]
        public FactorRange enemyFacingFactor;
        [Tooltip("In-range factor.")]
        public FactorRange rangeFactor;
        [Tooltip("Line-of-sight factor.")]
        public FactorRange losFactor;

        [Header("Range & Flanking")]
        [Tooltip("Distance at which to orbit the enemy.")]
        public float desiredRange;
        [Tooltip("Range tolerance for availability check.")]
        public float rangeMax;
        [Tooltip("Flanking offset angle in degrees from enemy's forward. 90 = perpendicular.")]
        public float flankAngleDeg;

        [Header("MPC Weight Overrides")]
        [Tooltip("Facing weight (moderate — fire while flanking).")]
        public float wFacing;
        [Tooltip("Exposure weight (moderate — avoid direct fire arc).")]
        public float wExposure;

        public static AttackFlankingTuning Default => new AttackFlankingTuning
        {
            healthFactor = new FactorRange(0.4f, 1.0f),
            shieldFactor = new FactorRange(0.5f, 1.0f),
            selfAngleFactor = new FactorRange(0.6f, 1.0f),
            enemyFacingFactor = new FactorRange(0.8f, 1.0f),
            rangeFactor = new FactorRange(0.5f, 1.2f),
            losFactor = new FactorRange(0.4f, 1.2f),
            desiredRange = 20f,
            rangeMax = 45f,
            flankAngleDeg = 75f,
            wFacing = 1.2f,
            wExposure = 1.5f,
        };
    }
}
