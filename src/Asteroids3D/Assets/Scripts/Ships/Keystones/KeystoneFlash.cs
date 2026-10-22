using Ships.Visuals;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// A keystone's tell: the hull holds a flash in the kit's colour while the kit is active. Rides the
    /// kit prefab, so the ship's presentation policy reaches it like any other part of the ship; the
    /// flash itself belongs to <see cref="HullVisuals"/>, the hull flash's single owner.
    /// </summary>
    public sealed class KeystoneFlash : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private Keystone keystone;
        [SerializeField] private Color color = Color.white;

        private HullVisuals hull;
        private bool held;

        private void Awake()
        {
            var ship = GetComponentInParent<Ship>();
            if (ship && ship.VisualRig) hull = ship.VisualRig.GetComponentInChildren<HullVisuals>(true);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable() => Hold(false);

        private void LateUpdate() => Hold(keystone.Active);

        private void Hold(bool on)
        {
            if (on == held || !hull) return;
            held = on;
            if (on) hull.HoldFlash(color);
            else hull.ReleaseFlash();
        }
    }
}
