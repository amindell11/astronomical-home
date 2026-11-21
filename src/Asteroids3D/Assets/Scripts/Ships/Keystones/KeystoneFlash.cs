using Ships.Visuals;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// A keystone's tell: the hull holds a flash in the kit's colour while the kit is active. A kit
    /// that holds the throttle first blinks the flash through the hold. Rides the kit prefab, so the
    /// ship's presentation policy reaches it like any other part of the ship; the flash itself belongs
    /// to <see cref="HullVisuals"/>, the hull flash's single owner.
    /// </summary>
    public sealed class KeystoneFlash : MonoBehaviour, IPresentationPart
    {
        private const float BlinkRateStart = 4f;
        private const float BlinkRateEnd = 14f;

        [SerializeField] private Keystone keystone;
        [SerializeField] private Color color = Color.white;

        private HullVisuals hull;
        private bool held;
        private float phase;

        private void Awake()
        {
            var ship = GetComponentInParent<Ship>();
            if (ship && ship.VisualRig) hull = ship.VisualRig.GetComponentInChildren<HullVisuals>(true);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            phase = 0f;
            Hold(false);
        }

        private void LateUpdate()
        {
            if (keystone.HoldsThrottle)
            {
                // Placeholder brake tell until the kit's own art lands.
                phase += Time.deltaTime * Mathf.Lerp(BlinkRateStart, BlinkRateEnd, keystone.HoldProgress);
                Hold(Mathf.Repeat(phase, 1f) < 0.5f);
                return;
            }

            phase = 0f;
            Hold(keystone.Active);
        }

        private void Hold(bool on)
        {
            if (on == held || !hull) return;
            held = on;
            if (on) hull.HoldFlash(color);
            else hull.ReleaseFlash();
        }
    }
}
