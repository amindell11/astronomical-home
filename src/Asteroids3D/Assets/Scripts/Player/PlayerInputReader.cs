using System;
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

        public float Thrust => Input.GetAxis(VerticalAxis);
        public float Strafe => Input.GetAxis(HorizontalAxis);
        public float Rotation => Input.GetAxis(RotationAxis);
        public bool BoostDown => Input.GetButtonDown(BoostButton);
        public bool PrimaryFire => Input.GetButton(Fire1Button);
        public bool SecondaryFireDown => Input.GetButtonDown(Fire2Button);
        public bool WantsToRotate => Input.GetButton(DirectionButton);

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
            return screenToGamePlane(Input.mousePosition);
        }
    }
}
