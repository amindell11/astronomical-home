using UnityEngine;

namespace Player
{
    /// <summary>
    /// Lightweight marker component placed on the player root GameObject.
    /// Used by trigger-based systems (KeyPickup, ExtractionZone) to identify
    /// the player entity via GetComponentInParent, regardless of which child
    /// collider triggers the event.
    /// </summary>
    public class PlayerMarker : MonoBehaviour { }
}
