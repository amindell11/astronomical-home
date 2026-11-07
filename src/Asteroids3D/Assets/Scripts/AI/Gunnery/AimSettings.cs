using UnityEngine;

namespace AI.Gunnery
{
    /// <summary>One pilot's aim knobs: how late and how loosely its Gunner sees the target, and its earned accuracy. The AICommander hands the asset to the Gunner, which reads it every tick, so edits in Play apply live. The servo's turn acceleration stays on <see cref="AI.Navigation.MPC.MpcSettings"/>.</summary>
    [CreateAssetMenu(fileName = "AimSettings", menuName = "AI/Aim Settings")]
    public class AimSettings : ScriptableObject
    {
        [Header("Perception")]
        [Tooltip("Seconds late the Gunner sees its target. 0 turns it off.")]
        [Min(0f)] public float reactionDelay = 0.375f;
        [Tooltip("RMS size (degrees) of a wandering bearing error on the target the Gunner sees. 0 turns it off.")]
        [Min(0f)] public float wanderSize = 11.25f;
        [Tooltip("Seconds over which the wander drifts to a fresh error. 0 turns the wander off.")]
        [Min(0f)] public float wanderCorrelationTime = 0.5f;

        [Header("Earned Accuracy")]
        [Tooltip("RMS bearing error (degrees) when the Gunner picks up a target. It tightens to 0 while the " +
                 "target stays tracked. 0 turns earned accuracy off.")]
        [Min(0f)] public float startingError;
        [Tooltip("Seconds of tracking that tighten the error from its start to 0.")]
        [Min(0.01f)] public float timeToTighten = 6f;
        [Tooltip("Tracking counts only while the target is within this angle (degrees) of the nose.")]
        [Min(0f)] public float cone = 30f;
        [Tooltip("Tracking counts only within this range (units). 0 removes the range gate.")]
        [Min(0f)] public float range = 25f;
        [Tooltip("Target acceleration across the line of sight (u/s²) that counts as a jink and resets the " +
                 "tracking. 0 turns the jink reset off.")]
        [Min(0f)] public float jinkThreshold = 5f;
        [Tooltip("A target boost resets the tracking.")]
        public bool resetOnBoost = true;
        [Tooltip("An asteroid breaking line of sight resets the tracking, and tracking grows only in line of sight.")]
        public bool resetOnLostLineOfSight = true;
    }
}
