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

        [Header("Tactics")]
        [Tooltip("Shield %, below which a ship hit within the last 'hit within' seconds breaks off.")]
        [Range(0f, 100f)] public float breakOffShield = 25f;
        [Tooltip("Shield %, at or above which a ship breaking off comes back to pick again.")]
        [Range(0f, 100f)] public float resumeShield = 80f;
        [Tooltip("Seconds since the ship was last hit within which a low shield makes it break off.")]
        [Min(0f)] public float hitWithin = 2f;
        [Tooltip("Shield %, below which a ship in the target's guns flanks or stands off instead of pressing.")]
        [Range(0f, 100f)] public float nerveShield = 50f;
        [Tooltip("Units outside the target's primary gun range at which a ship standing off waits.")]
        [Min(0f)] public float standOffMargin = 5f;
        [Tooltip("Seconds a ship holds every tactic it picks, break off included, before it can pick again.")]
        [Min(0f)] public float minimumHold = 2f;
    }
}
