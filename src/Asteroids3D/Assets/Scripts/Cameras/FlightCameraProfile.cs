using UnityEngine;

namespace Cameras
{
    [CreateAssetMenu(fileName = "FlightCamera", menuName = "Cameras/Flight Camera Profile")]
    public sealed class FlightCameraProfile : ScriptableObject
    {
        [Tooltip("Orthographic size while locked on the player: half the view height, in world units.")]
        [Min(0.01f)] public float zoom = 15f;

        [Tooltip("Seconds of the player's velocity the camera aims ahead. The follow smoothing trails the player by about its smoothTime, so the view leads by less.")]
        [Min(0f)] public float leadSeconds = 0.3f;

        [Tooltip("Cap on the lead distance, in world units.")]
        [Min(0f)] public float maxLead = 8f;

        [Tooltip("Seconds the lead takes to follow a change in the player's velocity.")]
        [Min(0f)] public float leadSmoothTime = 0.5f;
    }
}
