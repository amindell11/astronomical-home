using System;
using Ships.Visuals;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones.Tells
{
    /// <summary>
    /// On a press, the ship comes up through its arrival portal: the visual rig starts small and the
    /// hull black, both ease to normal over the opening, then the hull flashes a colour that fades
    /// out. Rides the kit prefab.
    /// </summary>
    public sealed class KeystoneEmergence : MonoBehaviour, IPresentationPart
    {
        [Tooltip("The kit whose presses start this arrival.")]
        [SerializeField] private Keystone keystone;

        [Tooltip("Seconds from the press until the ship is full size and its own colour. 0 skips to the flash.")]
        [SerializeField] private float opening = 0.2f;

        [Tooltip("Size of the visual rig at the press, as a share of its authored size.")]
        [SerializeField, Range(0.05f, 1f)] private float startScale = 0.15f;

        [Tooltip("Hull colour through the opening.")]
        [SerializeField] private Color dark = Color.black;

        [Tooltip("Hull colour that fades out after the opening.")]
        [SerializeField] private Color flash = new(0.89f, 0.82f, 1f);

        [Tooltip("Seconds the flash takes to fade after the opening. 0 skips the flash.")]
        [SerializeField] private float flashTime = 0.15f;

        private Transform rig;
        private Vector3 authoredScale;
        private HullVisuals hull;
        private int seenPresses;
        private float startedAt;
        private bool playing;

        private void Awake()
        {
            if (!keystone)
                throw new InvalidOperationException($"KeystoneEmergence on '{name}' needs its keystone.");
            seenPresses = keystone.TelledPresses;

            var ship = GetComponentInParent<Ship>();
            if (!ship || !ship.VisualRig) return;
            rig = ship.VisualRig.transform;
            authoredScale = rig.localScale;
            hull = ship.VisualRig.GetComponentInChildren<HullVisuals>(true);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            if (!playing) return;
            playing = false;
            if (rig) rig.localScale = authoredScale;
            if (hull) hull.ReleaseFlash(this);
        }

        private void LateUpdate()
        {
            if (!hull) return;

            if (keystone.TelledPresses > seenPresses)
            {
                startedAt = Time.time;
                playing = true;
            }

            seenPresses = keystone.TelledPresses;
            if (!playing) return;

            var t = Time.time - startedAt;
            if (opening > 0f && t < opening)
            {
                var u = t / opening;
                var e = 1f - (1f - u) * (1f - u);
                rig.localScale = authoredScale * Mathf.Lerp(startScale, 1f, e);
                hull.HoldFlash(this, dark, 1f - e);
                return;
            }

            rig.localScale = authoredScale;
            var flashed = opening > 0f ? t - opening : t;
            if (flashTime > 0f && flashed < flashTime)
            {
                hull.HoldFlash(this, flash, 1f - flashed / flashTime);
                return;
            }

            hull.ReleaseFlash(this);
            playing = false;
        }
    }
}
