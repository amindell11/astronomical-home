using Ships.Visuals;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// A keystone's tell: the hull holds a flash in the kit's colour while the kit is active, then fades it over the release. Rides the
    /// kit prefab, so the ship's presentation policy reaches it like any other part of the ship; the
    /// flash itself belongs to <see cref="HullVisuals"/>, the hull flash's single owner.
    /// </summary>
    public sealed class KeystoneFlash : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private Keystone keystone;
        [SerializeField] private Color color = Color.white;

        [Tooltip("Seconds the flash takes to fade after the kit stops being active. 0 snaps off.")]
        [SerializeField] private float release;

        [Tooltip("1 fades evenly. Higher holds then drops; lower drops first then trails.")]
        [SerializeField] private float releaseCurve = 1f;

        private HullVisuals hull;
        private bool held;
        private float releasedFor;

        public float Release => release;

        private void Awake()
        {
            var ship = GetComponentInParent<Ship>();
            if (ship && ship.VisualRig) hull = ship.VisualRig.GetComponentInChildren<HullVisuals>(true);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable() => StopFlash();

        private void LateUpdate()
        {
            if (!hull) return;

            if (keystone.Active)
            {
                if (!held || releasedFor > 0f) hull.HoldFlash(color);
                held = true;
                releasedFor = 0f;
                return;
            }

            if (!held) return;

            releasedFor += Time.deltaTime;
            if (releasedFor >= release)
            {
                StopFlash();
                return;
            }

            hull.HoldFlash(color, 1f - Mathf.Pow(releasedFor / release, releaseCurve));
        }

        private void StopFlash()
        {
            if (held && hull) hull.ReleaseFlash();
            held = false;
            releasedFor = 0f;
        }
    }
}
