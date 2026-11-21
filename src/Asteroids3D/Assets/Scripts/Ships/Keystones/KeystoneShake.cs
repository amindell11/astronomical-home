using Cameras.Shake;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// Each activation raises the kit's shake on its ship's shake channel, along the ship's heading,
    /// and the end of the kit's active window releases it. Rides the kit prefab, so presentation reaches it.
    /// </summary>
    public sealed class KeystoneShake : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private Keystone keystone;

        [Tooltip("Shake played on the ship at each activation, released when the kit's active window ends.")]
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
            if (!ship) return;
            ship.Shake.Release(this);
        }

        private void LateUpdate()
        {
            // A kit instantiated without a ship, as tests do, has nothing to shake.
            if (!ship) return;
            if (keystone.Activations > seenActivations)
            {
                ship.Shake.Raise(this, profile, ship.Kinematics.Forward);
                held = true;
            }

            seenActivations = keystone.Activations;
            if (!held || keystone.Active) return;
            ship.Shake.Release(this);
            held = false;
        }
    }
}
