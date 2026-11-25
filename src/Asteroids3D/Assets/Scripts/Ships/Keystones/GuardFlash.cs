using Ships.Keystones.Kits;
using Ships.Visuals;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// The Guard Dash tell: the hull holds a white flash for exactly the guard window. Rides the kit
    /// prefab, so the ship's presentation policy reaches it like any other part of the ship; the
    /// flash itself belongs to <see cref="HullVisuals"/>, the hull flash's single owner.
    /// </summary>
    public sealed class GuardFlash : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private GuardDash guard;

        private HullVisuals hull;
        private bool held;

        private void Awake()
        {
            var ship = GetComponentInParent<Ship>();
            if (ship && ship.VisualRig) hull = ship.VisualRig.GetComponentInChildren<HullVisuals>(true);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable() => Hold(false);

        private void LateUpdate() => Hold(guard.Guarding);

        private void Hold(bool on)
        {
            if (on == held || !hull) return;
            held = on;
            if (on) hull.HoldFlash(Color.white);
            else hull.ReleaseFlash();
        }
    }
}
