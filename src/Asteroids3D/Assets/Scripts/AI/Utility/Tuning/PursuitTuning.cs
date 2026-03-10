using System;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct PursuitTuning
    {
        [Header("Utility Factors")]
        [Tooltip("Utility weight based on self health. Low HP = less eager to pursue.")]
        public FactorRange healthFactor;
        [Tooltip("Utility weight based on self shield.")]
        public FactorRange shieldFactor;
        [Tooltip("Utility weight based on enemy distance (higher = more eager when far).")]
        public FactorRange distanceFactor;
        [Tooltip("Utility penalty when outnumbered.")]
        public FactorRange threatFactor;

        [Header("Range")]
        [Tooltip("Distance at which pursuit activates (should match or exceed attack optimalRangeMax).")]
        public float engageDistance;

        public static PursuitTuning Default => new PursuitTuning
        {
            healthFactor = new FactorRange(0.4f, 1.0f),
            shieldFactor = new FactorRange(0.5f, 1.0f),
            distanceFactor = new FactorRange(0.3f, 1.2f),
            threatFactor = new FactorRange(1.0f, 0.5f),
            engageDistance = 50f,
        };
    }
}
