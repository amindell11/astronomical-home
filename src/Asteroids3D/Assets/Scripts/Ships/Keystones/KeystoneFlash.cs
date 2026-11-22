using Ships.Visuals;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// A keystone's tell: the hull holds a flash in the kit's colour while the kit is active, then
    /// fades it over the release. A kit that holds the throttle first blinks the flash through the
    /// hold. Rides the kit prefab, so the ship's presentation policy reaches it like any other part of
    /// the ship; the flash itself belongs to <see cref="HullVisuals"/>, the hull flash's single owner.
    /// </summary>
    public sealed class KeystoneFlash : MonoBehaviour, IPresentationPart
    {
        private const float BlinkRateStart = 4f;
        private const float BlinkRateEnd = 14f;

        [SerializeField] private Keystone keystone;
        [SerializeField] private Color color = Color.white;

        [Tooltip("Seconds the flash takes to fade after the kit stops being active. 0 snaps off.")]
        [SerializeField] private float release;

        [Tooltip("1 fades evenly. Higher holds then drops; lower drops first then trails.")]
        [SerializeField] private float releaseCurve = 1f;

        private HullVisuals hull;
        private bool held;
        private float phase;
        private float releasedFor;

        public float Release => release;

        private void Awake()
        {
            var ship = GetComponentInParent<Ship>();
            if (ship && ship.VisualRig) hull = ship.VisualRig.GetComponentInChildren<HullVisuals>(true);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            phase = 0f;
            if (hull) Hold(false);
        }

        private void LateUpdate()
        {
            if (!hull) return;

            if (keystone.HoldsThrottle)
            {
                // Placeholder brake tell until the kit's own art lands.
                phase += Time.deltaTime * Mathf.Lerp(BlinkRateStart, BlinkRateEnd, keystone.HoldProgress);
                Hold(Mathf.Repeat(phase, 1f) < 0.5f);
                return;
            }

            phase = 0f;
            if (keystone.Active)
            {
                Hold(true);
                return;
            }

            if (!held) return;

            releasedFor += Time.deltaTime;
            if (releasedFor >= release)
            {
                Hold(false);
                return;
            }

            hull.HoldFlash(color, 1f - Mathf.Pow(releasedFor / release, releaseCurve));
        }

        private void Hold(bool on)
        {
            if (on && (!held || releasedFor > 0f)) hull.HoldFlash(color);
            if (!on && held) hull.ReleaseFlash();
            held = on;
            releasedFor = 0f;
        }
    }
}
