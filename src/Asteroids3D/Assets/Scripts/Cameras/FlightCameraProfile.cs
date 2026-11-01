using UnityEngine;

namespace Cameras
{
    [CreateAssetMenu(fileName = "FlightCamera", menuName = "Cameras/Flight Camera Profile")]
    public sealed class FlightCameraProfile : ScriptableObject
    {
        [Tooltip("Narrowest orthographic size while locked on the player: half the view height, in world units. Speed and nearby enemies widen it from here.")]
        [Min(0.01f)] public float zoom = 15f;

        [Tooltip("How much wider than zoom the locked view may get, in orthographic size units.")]
        [Min(0f)] public float maxWiden = 7f;

        [Tooltip("Extra orthographic size per unit of the player's speed.")]
        [Min(0f)] public float zoomPerSpeed = 0.15f;

        [Tooltip("Enemies closer to the player than this count as being fought, and the view widens to keep them in frame. In world units.")]
        [Min(0f)] public float fightRadius = 30f;

        [Tooltip("An enemy being fought stops counting once it is this far beyond fightRadius, in world units.")]
        [Min(0f)] public float releaseMargin = 10f;

        [Tooltip("Seconds the zoom takes to widen.")]
        [Min(0f)] public float widenTime = 0.4f;

        [Tooltip("Seconds the zoom takes to narrow back.")]
        [Min(0f)] public float narrowTime = 1.5f;

        [Tooltip("Seconds of the player's velocity the camera aims ahead. The follow smoothing trails the player by about its smoothTime, so the view leads by less.")]
        [Min(0f)] public float leadSeconds = 0.3f;

        [Tooltip("Cap on the lead distance, in world units.")]
        [Min(0f)] public float maxLead = 8f;

        [Tooltip("Seconds the lead takes to follow a change in the player's velocity.")]
        [Min(0f)] public float leadSmoothTime = 0.5f;
    }
}
