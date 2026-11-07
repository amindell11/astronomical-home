using UnityEngine;

namespace AI.Tactics
{
    /// <summary>Every setting of the brain that picks tactics lives on this one asset, a header per piece.</summary>
    [CreateAssetMenu(fileName = "TacticsSettings", menuName = "AI/Tactics Settings")]
    public class TacticsSettings : ScriptableObject
    {
        [Header("Situation")]
        [Tooltip("Full width, in degrees, of the cone off the target's nose. A ship inside it and inside the " +
                 "target's primary gun range is exposed, and a side of the target inside it is never open.")]
        [Range(0f, 360f)] public float exposureCone = 60f;
        [Tooltip("Width, in units, of a ship's firing lane to its target. An ally whose centre sits inside it, " +
                 "between the ship and the target, is in the lane.")]
        [Min(0f)] public float laneWidth = 4f;
    }
}
