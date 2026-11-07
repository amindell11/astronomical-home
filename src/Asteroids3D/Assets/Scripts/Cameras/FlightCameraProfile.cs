using UnityEngine;

namespace Cameras
{
    [CreateAssetMenu(fileName = "FlightCamera", menuName = "Cameras/Flight Camera Profile")]
    public sealed class FlightCameraProfile : ScriptableObject
    {
        [Tooltip("Narrowest orthographic size while locked on the player: half the view height, in world units. Speed and nearby enemies widen it from here.")]
        [Min(0.01f)] public float zoom = 15f;

        [Tooltip("How much wider than zoom speed may widen the locked view, in orthographic size units.")]
        [Min(0f)] public float maxWiden = 7f;

        [Tooltip("Extra orthographic size per unit of the player's speed.")]
        [Min(0f)] public float zoomPerSpeed = 0.15f;

        [Tooltip("Enemies closer to the player than this count as being fought, and the view widens and pulls toward them to keep them in frame. In world units.")]
        [Min(0f)] public float fightRadius = 30f;

        [Tooltip("An enemy being fought stops counting once it is this far beyond fightRadius, in world units.")]
        [Min(0f)] public float releaseMargin = 10f;

        [Tooltip("How much wider than zoom the view may get to fit the enemies being fought, in orthographic size units.")]
        [Min(0f)] public float fightMaxWiden = 7f;

        [Tooltip("How hard the camera pulls toward the enemies being fought, from 0 to 1. At 1 it shifts just enough to fit them within fightMaxWiden; at 0 it never shifts.")]
        [Range(0f, 1f)] public float fightPull = 1f;

        [Tooltip("Seconds the zoom takes to widen.")]
        [Min(0f)] public float widenTime = 0.4f;

        [Tooltip("Seconds the zoom takes to narrow back.")]
        [Min(0f)] public float narrowTime = 1.5f;

        [Tooltip("Seconds of the player's velocity the camera aims ahead. The follow smoothing trails the player by about its smoothTime, so the view leads by less.")]
        [Min(0f)] public float leadSeconds = 0.3f;

        [Tooltip("Cap on the lead distance, in world units.")]
        [Min(0f)] public float maxLead = 8f;

        [Tooltip("Seconds the velocity lead takes to follow a change in the player's velocity, and the fight pull a change in the fight.")]
        [Min(0f)] public float leadSmoothTime = 0.5f;

        [Tooltip("How far the camera leads toward the cursor with it at the screen edge, in world units. Adds to the velocity lead.")]
        [Min(0f)] public float cursorLead = 6f;

        [Tooltip("Fraction of half the screen around its center where the cursor leads nothing. Past it the lead ramps up to full at the screen edge.")]
        [Range(0f, 1f)] public float cursorDeadZone = 0.2f;

        [Tooltip("Seconds the cursor lead takes to follow the cursor.")]
        [Min(0f)] public float cursorLeadSmoothTime = 0.4f;
    }
}
