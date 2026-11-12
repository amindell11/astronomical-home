using Balance;
using UnityEngine;

namespace Substrate.Sectors.Elements.Hunt
{
    [CreateAssetMenu(fileName = "HuntFeel", menuName = "Sectors/Hunt Feel Profile")]
    public sealed class HuntFeelProfile : ScriptableObject
    {
        [Tooltip("Seconds from the hunt's start to the alarm when the spotted meter never fills.")]
        [Stat, Min(0f)] public float searchSeconds = 45f;

        [Tooltip("Distance, in units, at which a patrol can see the player inside its forward cone, given no asteroid in between.")]
        [Stat, Min(0f)] public float sightRadius = 50f;

        [Tooltip("Full angle, in degrees, of a patrol's forward sight cone.")]
        [Stat, Range(0f, 360f)] public float coneDegrees = 90f;

        [Tooltip("Distance, in units, inside which a patrol sees the player in every direction.")]
        [Stat, Min(0f)] public float closeSightRadius = 12f;

        [Tooltip("Seconds of continuous sight that fill the spotted meter from empty. Patrols that see the player do not stack.")]
        [Stat, Min(0f)] public float spotFillSeconds = 2f;

        [Tooltip("Seconds for a full spotted meter to drain once no patrol sees the player, and how long a seeker searches after losing the player.")]
        [Stat, Min(0f)] public float spotDrainSeconds = 6f;

        [Tooltip("Share of the spotted meter a hit by the player on a patrol adds. 1 alarms on that hit.")]
        [Stat, Range(0f, 1f)] public float hitFill = .5f;

        [Tooltip("Seconds from the alarm to the first wave. Stations stay lit until then.")]
        [Stat, Min(0f)] public float alarmGraceSeconds = 5f;

        [Tooltip("Speed, in units per second, an unaware patrol flies its route at. Keep it below the patrol ship's top speed.")]
        [Stat, Min(0f)] public float routeSpeed = 8f;

        [Tooltip("Speed, in units per second, a patrol chases and searches at. Keep it below the player's top speed so the player can outrun it.")]
        [Stat, Min(0f)] public float seekSpeed = 14f;

        [Tooltip("Distance, in units, past the last-seen point, along the player's last heading, that a search centers on.")]
        [Stat, Min(0f)] public float followDistance = 30f;

        [Tooltip("Radius, in units, of the loop a searching patrol flies around the search center.")]
        [Stat, Min(0f)] public float searchRadius = 25f;

        [Tooltip("Distance, in units, beside the player's start that a patrol's route passes. Keep it inside the sight radius.")]
        [Stat, Min(0f)] public float routeStartPass = 25f;

        [Tooltip("Distance, in units, at which a patrol counts a route waypoint as reached and turns for the next.")]
        [Stat, Min(0f)] public float arrivalRadius = 15f;
    }
}
