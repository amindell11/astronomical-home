using Game;
using UnityEngine;

namespace Player
{
    public class PlayerInputReader
    {
        private const string VerticalAxis = "Vertical";
        private const string HorizontalAxis = "Horizontal";
        private const string RotationAxis = "Rotation";
        private const string BoostButton = "Boost";
        private const string Fire1Button = "Fire1";
        private const string Fire2Button = "Fire2";
        private const string DirectionButton = "Direction";

        public static float Thrust => Input.GetAxis(VerticalAxis);
        public static float Strafe => Input.GetAxis(HorizontalAxis);
        public static float Rotation => Input.GetAxis(RotationAxis);
        public static bool BoostDown => Input.GetButtonDown(BoostButton);
        public static bool PrimaryFire => Input.GetButton(Fire1Button);
        public static bool SecondaryFireDown => Input.GetButtonDown(Fire2Button);
        public static bool WantsToRotate => Input.GetButton(DirectionButton);

        public static Vector3 GetMouseWorldPosition()
        {
            var mainCamera = Camera.main;
            if (!mainCamera) return Vector3.zero;
            
            return GamePlane.ProjectOntoPlane(
                mainCamera.ScreenToWorldPoint(Input.mousePosition));
        }
    }
}
