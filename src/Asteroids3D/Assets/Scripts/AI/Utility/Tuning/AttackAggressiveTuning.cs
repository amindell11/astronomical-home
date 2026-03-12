using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct AttackAggressiveTuning
    {
        [Header("Utility Factors")]
        [Tooltip("Health weight. High HP = more eager for aggressive stance.")]
        public FactorRange healthFactor;
        [Tooltip("Shield weight.")]
        public FactorRange shieldFactor;
        [Tooltip("Boost when enemy is weak (finishing blow).")]
        public FactorRange enemyWeakFactor;
        [Tooltip("Boost when enemy is facing away (positional advantage). Input: EnemyFacingThreat.")]
        public FactorRange enemyFacingFactor;
        [Tooltip("Boost when we face the enemy (low self-angle = advantage). Input: SelfAngleNorm.")]
        public FactorRange selfAngleFactor;
        [Tooltip("In-range factor.")]
        public FactorRange rangeFactor;
        [Tooltip("Line-of-sight factor.")]
        public FactorRange losFactor;

        [Header("Range")]
        [Tooltip("Inner edge of tight MPC MaintainRange band.")]
        public float optimalRangeMin;
        [Tooltip("Outer edge of tight MPC MaintainRange band.")]
        public float optimalRangeMax;

        [Header("MPC Weight Overrides")]
        [Tooltip("Facing weight override (high for aggressive aim).")]
        public float wFacing;
        [Tooltip("Exposure weight override (low — willing to tank hits).")]
        public float wExposure;
        [Tooltip("Facing precision override.")]
        public float facingWidth;

        public static AttackAggressiveTuning Default => new AttackAggressiveTuning
        {
            healthFactor = new FactorRange(0.3f, 1.2f),
            shieldFactor = new FactorRange(0.4f, 1.1f),
            enemyWeakFactor = new FactorRange(1.3f, 1.0f),
            enemyFacingFactor = new FactorRange(1.2f, 0.5f), // Inverted: low threat = high utility
            selfAngleFactor = new FactorRange(1.2f, 0.5f),   // Inverted: low angle = we face enemy = high utility
            rangeFactor = new FactorRange(0.5f, 1.2f),
            losFactor = new FactorRange(0.4f, 1.2f),
            optimalRangeMin = 6f,
            optimalRangeMax = 25f,
            wFacing = 2.0f,
            wExposure = 0.5f,
            facingWidth = 0.3f,
        };
    }
}
