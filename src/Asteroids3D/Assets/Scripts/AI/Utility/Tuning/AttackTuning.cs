using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct AttackTuning
    {
        [Header("Utility Factors")]
        [Tooltip("Utility weight based on self health. Low HP = less eager to attack.")]
        public FactorRange healthFactor;
        [Tooltip("Utility weight based on self shield. Low shield = less eager to attack.")]
        public FactorRange shieldFactor;
        [Tooltip("Utility boost when enemy combined durability is low (finishing blow incentive).")]
        public FactorRange enemyWeakFactor;
        [Tooltip("Utility weight based on whether the enemy is within optimal range.")]
        public FactorRange rangeFactor;
        [Tooltip("Utility weight based on line-of-sight to the enemy.")]
        public FactorRange losFactor;
        [Tooltip("Utility penalty when outnumbered (more enemies nearby).")]
        public FactorRange threatFactor;
        [Tooltip("Utility boost at low HP to commit to the fight rather than flee.")]
        public FactorRange desperationFactor;

        [Header("Range")]
        [Tooltip("Inner edge of the MPC MaintainRange band around the enemy.")]
        public float optimalRangeMin;
        [Tooltip("Outer edge of the MPC MaintainRange band around the enemy.")]
        public float optimalRangeMax;
        [Tooltip("Distance beyond which the outerRange utility factor kicks in.")]
        public float outerDistanceThreshold;
        [Tooltip("Utility multiplier applied when enemy is beyond outerDistanceThreshold.")]
        public float outerRangeFactor;

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
        };
    }
}
