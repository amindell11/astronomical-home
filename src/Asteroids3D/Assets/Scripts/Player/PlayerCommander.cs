using System;
using Ships;
using Ships.Command;
using UnityEngine;
using Game;

namespace Player
{
    /// <summary>
    /// Translates player input into commands for the Ship component.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public partial class PlayerCommander : Commander
    {
        [Header("Settings")]
        [Tooltip("If checked, the ship will rotate towards the mouse position. If unchecked, the ship will rotate using the rotation input axis.")]
        [SerializeField] private bool useMouseDirection = false;

        [Header("Gizmo Settings")]
        [SerializeField] private bool showMouseGizmos = true;
        [SerializeField] private float mouseGizmoScale = 3f;

        private Ship ship;
        private PlayerInputReader playerInput;
        private bool hasScreenProjector;

        private Vector3 directionToMouse;
        private Vector3 projectedDirection;
        private float targetAngle;

        public bool HasScreenProjectorConfigured => hasScreenProjector;

        private void Awake()
        {
            playerInput = new PlayerInputReader(_ => Vector3.zero);
        }

        public void SetScreenToGamePlane(Func<Vector3, Vector3> screenToGamePlane)
        {
            playerInput.SetScreenToGamePlane(screenToGamePlane);
            hasScreenProjector = true;
        }

        public override void InitializeCommander(Ship ship)
        {
            this.ship = ship;
        }

        private float thrustInput;
        private float strafeInput;
        private float rotationInput;
        private bool primaryInput;
        private bool secondaryInput;
        private bool boostInput;
        private bool wantsRotate;

        private void Update()
        {
            if (!ship) return;

            thrustInput = playerInput.Thrust;
            strafeInput = playerInput.Strafe;
            rotationInput = playerInput.Rotation;
            boostInput = playerInput.BoostDown;
            primaryInput = playerInput.PrimaryFire;
            secondaryInput = playerInput.SecondaryFireDown;
            wantsRotate = playerInput.WantsToRotate;

            if (useMouseDirection && wantsRotate)
            {
                var mouseWorldPos = playerInput.GetMouseWorldPosition();
                directionToMouse = (mouseWorldPos - ship.transform.position).normalized;
                targetAngle = CalculateYawAngle(directionToMouse);
            }

            cachedCommand.thrust = thrustInput;
            cachedCommand.strafe = strafeInput;
            cachedCommand.boost = (boostInput && ship.Movement.BoostAvailable) ? 1f : 0f;
            cachedCommand.primaryFire = primaryInput;
            cachedCommand.secondaryFire = secondaryInput;
        }

        private void FixedUpdate()
        {
            if (!ship) return;

            cachedCommand.yawTorque = useMouseDirection
                ? (wantsRotate ? GetMouseRotationTorque() : 0)
                : rotationInput;
        }

        private float GetMouseRotationTorque()
        {
            var kin = ship.Movement.Kinematics;
            return Ships.Movement.ControlUtils.RotationPd(targetAngle, kin.yaw, kin.yawRate, ship.settings.maxYawRate, 4f);
        }

        private float CalculateYawAngle(Vector3 direction)
        {
            var planeNormal = GamePlane.Normal;
            projectedDirection = Vector3.ProjectOnPlane(direction, planeNormal).normalized;
            var angle = Vector3.SignedAngle(GamePlane.Forward, projectedDirection, planeNormal);

            if (angle < 0) angle += 360f;

            return angle;
        }
    }
}
