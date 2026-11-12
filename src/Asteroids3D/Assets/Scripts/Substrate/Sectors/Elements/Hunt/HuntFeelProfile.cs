using Balance;
using UnityEngine;

namespace Substrate.Sectors.Elements.Hunt
{
    [CreateAssetMenu(fileName = "HuntFeel", menuName = "Sectors/Hunt Feel Profile")]
    public sealed class HuntFeelProfile : ScriptableObject
    {
        [Tooltip("Distance, in units, at which a patrol sees the player, given no asteroid in between.")]
        [Stat, Min(0f)] public float sightRadius = 50f;

        [Tooltip("Seconds from a patrol first seeing the player to the alarm. Killing the patrol in that time cancels it.")]
        [Stat, Min(0f)] public float callInSeconds = 3f;

        [Tooltip("Speed, in units per second, an unaware patrol flies its route at. Keep it below the patrol ship's top speed.")]
        [Stat, Min(0f)] public float routeSpeed = 8f;

        [Tooltip("Distance, in units, beside the player's start that a patrol's route passes. Keep it inside the sight radius.")]
        [Stat, Min(0f)] public float routeStartPass = 25f;

        [Tooltip("Distance, in units, at which a patrol counts a route waypoint as reached and turns for the next.")]
        [Stat, Min(0f)] public float arrivalRadius = 15f;
    }
}
