using System;
using UnityEngine;

namespace Game.Player
{
    public class PlayerInputReader : IDisposable
    {
        private readonly PlayerControls controls = new();

        public float Thrust => controls.Flight.Thrust.ReadValue<float>();
        public float Strafe => controls.Flight.Strafe.ReadValue<float>();
        public bool BoostDown => controls.Flight.Boost.WasPressedThisFrame();
        // Both triggers report the held (level) state; the weapon decides auto vs semi-auto,
        // and PlayerCommander derives the press edge for semi-auto weapons.
        public bool PrimaryFire => controls.Flight.PrimaryFire.IsPressed();
        public bool SecondaryFire => controls.Flight.SecondaryFire.IsPressed();
        public bool WantsToRotate => !controls.Flight.HoldHeading.IsPressed();

        private Func<Vector3, Vector3> screenToGamePlane;

        public PlayerInputReader(Func<Vector3, Vector3> screenToGamePlane)
        {
            SetScreenToGamePlane(screenToGamePlane);
        }

        public void SetScreenToGamePlane(Func<Vector3, Vector3> projector)
        {
            screenToGamePlane = projector ?? throw new ArgumentNullException(nameof(projector));
        }

        public Vector3 GetMouseWorldPosition()
        {
            return screenToGamePlane(controls.Flight.AimPoint.ReadValue<Vector2>());
        }

        public void Enable() => controls.Flight.Enable();

        public void Disable() => controls.Flight.Disable();

        public void Dispose() => controls.Dispose();
    }
}
