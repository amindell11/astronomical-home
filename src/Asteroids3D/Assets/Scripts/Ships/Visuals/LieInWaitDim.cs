using UnityEngine;

namespace Ships.Visuals
{
    /// <summary>Lie in wait's tell: the hull shades dark while the ship lies hidden.</summary>
    [RequireComponent(typeof(HullVisuals))]
    public sealed class LieInWaitDim : MonoBehaviour
    {
        [Tooltip("Hull colour the ship shades toward while it lies in wait.")]
        [SerializeField] private Color dim = Color.black;

        [Tooltip("How far the hull shades toward the dim colour, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float strength = 0.5f;

        private Ship ship;
        private HullVisuals hull;
        private bool held;

        private void Awake()
        {
            ship = GetComponentInParent<Ship>();
            hull = GetComponent<HullVisuals>();
        }

        private void OnDisable() => Hold(false);

        // The hangar preview's rig has no ship.
        private void LateUpdate()
        {
            if (ship) Hold(ship.LieInWait.Hiding);
        }

        private void Hold(bool on)
        {
            if (on == held) return;
            held = on;
            if (on) hull.HoldFlash(this, dim, strength);
            else hull.ReleaseFlash(this);
        }
    }
}
