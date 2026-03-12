using UnityEngine;

namespace AI.Utility
{
    /// <summary>
    /// Centralized tuning parameters for AI behavior.
    /// Per-state structs define how conditions affect utility scores using geometric mean.
    /// </summary>
    [CreateAssetMenu(fileName = "UtilityTuning", menuName = "AI/Utility Tuning")]
    public class UtilityTuning : ScriptableObject
    {
        [Header("State Weights (Shared Baseline)")]
        public UtilityWeights utilityWeights;

        [Header("Combat Assessment")]
        public float combatExitDelay = 3f;

        [Header("Per-State Tuning")]
        public AttackTuning attack = AttackTuning.Default;
        public AttackAggressiveTuning attackAggressive = AttackAggressiveTuning.Default;
        public AttackEvasiveTuning attackEvasive = AttackEvasiveTuning.Default;
        public PursuitTuning pursuit = PursuitTuning.Default;
        public EvadeTuning evade = EvadeTuning.Default;
        public PatrolTuning patrol = PatrolTuning.Default;
    }
}
