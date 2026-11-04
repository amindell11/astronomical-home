using Balance;
using UnityEngine;

namespace Substrate.Sectors.Elements.Hunt
{
    [CreateAssetMenu(fileName = "HuntFeel", menuName = "Sectors/Hunt Feel Profile")]
    public sealed class HuntFeelProfile : ScriptableObject
    {
        [Tooltip("Seconds the hunt lasts with no patrol calling the player in; then the alarm sounds anyway.")]
        [Stat, Min(0f)] public float searchSeconds = 45f;

        [Tooltip("Distance, in units, at which a patrol sees the player, given no asteroid in between.")]
        [Stat, Min(0f)] public float sightRadius = 50f;

        [Tooltip("Seconds from a patrol first seeing the player to the alarm. Killing the patrol in that time cancels it.")]
        [Stat, Min(0f)] public float callInSeconds = 3f;
    }
}
