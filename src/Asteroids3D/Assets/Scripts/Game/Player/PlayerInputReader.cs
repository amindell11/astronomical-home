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
        // Shift+R also matches the bare R binding; the shifted reload claims the press.
        public bool PrimaryReload => controls.Flight.PrimaryReload.WasPressedThisFrame() && !SecondaryReload;
        public bool SecondaryReload => controls.Flight.SecondaryReload.WasPressedThisFrame();
        public bool WantsToRotate => !controls.Flight.HoldHeading.IsPressed();
        public bool AssistTogglePressed => controls.Flight.ToggleAssist.WasPressedThisFrame();
        // Raised inside the input update, so a jump commits before the next physics step.
        public event Action JumpPressed;

        private Func<Vector3, Vector3> screenToGamePlane;

        public PlayerInputReader(Func<Vector3, Vector3> screenToGamePlane)
        {
            SetScreenToGamePlane(screenToGamePlane);
            controls.Flight.Jump.performed += _ => JumpPressed?.Invoke();
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
