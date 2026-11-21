using Cameras.Shake;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// An activation raises the kit's shake on its ship's shake channel, and every frame scales it by the ship's speed,
    /// so it only buzzes once the ship runs fast. It releases once the kit is idle and the ship has slowed off the curve.
    /// </summary>
    public sealed class KeystoneSpeedShake : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private Keystone keystone;

        [Tooltip("Shake raised when the kit activates; its strength then follows the profile's bySpeed curve.")]
        [SerializeField] private CameraShakeProfile profile;

        private Ship ship;
        private int seenActivations;
        private bool held;

        private void Awake() => ship = GetComponentInParent<Ship>();

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnEnable() => seenActivations = keystone.Activations;

        private void OnDisable()
        {
            if (!held) return;
            held = false;
            ship.Shake.Release(this);
        }

        private void LateUpdate()
        {
            // A kit instantiated without a ship, as tests do, has nothing to shake.
            if (!ship) return;
            if (keystone.Activations > seenActivations)
            {
                ship.Shake.Raise(this, profile, Vector2.zero);
                held = true;
            }

            seenActivations = keystone.Activations;
            if (!held) return;
            var strength = Mathf.Clamp01(profile.bySpeed.Evaluate(ship.Kinematics.Speed / ship.MaxSpeed));
            ship.Shake.Scale(this, strength);
            if (keystone.Active || strength > 0f) return;
            ship.Shake.Release(this);
            held = false;
        }
    }
}
