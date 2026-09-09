using System;
using Ships.Command;
using UnityEngine;
using Game;

namespace Player
{
    /// <summary>
    /// Translates player input into piloting and firing commands, pushed to the injected
    /// <see cref="IPilot"/>/<see cref="IWeapons"/> actuators. Reads ship situation through the
    /// narrow <see cref="IShipStatus"/> — it never touches the Ship directly.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class PlayerCommander : Commander
    {
        [Header("Settings")]
        [Tooltip("If checked, the ship will rotate towards the mouse position. If unchecked, the ship will rotate using the rotation input axis.")]
        [SerializeField] internal bool useMouseDirection = false;

        [Header("Gizmo Settings")]
        [SerializeField] internal float mouseGizmoScale = 3f;

        private IShipStatus context;
        private IPilot pilot;
        private IWeapons weapons;
        internal PlayerInputReader playerInput;
        private bool hasScreenProjector;

        internal Vector3 directionToMouse;
        internal Vector3 projectedDirection;
        private float targetAngle;

        public bool HasScreenProjectorConfigured => hasScreenProjector;

        private void Awake()
        {
            playerInput = new PlayerInputReader(_ => throw new InvalidOperationException("Screen-to-game-plane projector has not been configured."));
        }

        public void SetScreenToGamePlane(Func<Vector3, Vector3> screenToGamePlane)
        {
            playerInput.SetScreenToGamePlane(screenToGamePlane);
            hasScreenProjector = true;
        }

        public override void Initialize(in ShipControl control)
        {
            context = control.Ship;
            pilot = control.Pilot;
            weapons = control.WeaponActuator;
        }

        private float thrustInput;
        private float strafeInput;
        private float rotationInput;
        private bool primaryHeld;
        private bool secondaryHeld;
        private bool prevPrimaryHeld;
        private bool prevSecondaryHeld;
        private bool boostInput;
        private bool wantsRotate;

        private void Update()
        {
            if (context == null) return;

            thrustInput = playerInput.Thrust;
            strafeInput = playerInput.Strafe;
            rotationInput = playerInput.Rotation;
            boostInput |= playerInput.BoostDown;
            primaryHeld = playerInput.PrimaryFire;
            secondaryHeld = playerInput.SecondaryFire;
            wantsRotate = playerInput.WantsToRotate;

            if (useMouseDirection && wantsRotate && hasScreenProjector)
            {
                var mouseWorldPos = playerInput.GetMouseWorldPosition();
                directionToMouse = (mouseWorldPos - context.Transform.position).normalized;
                targetAngle = CalculateYawAngle(directionToMouse);
            }
        }

        // MovementController latches the last pilot command and charge weapons fire on a
        // trigger-up step — release everything this commander drives when it goes silent.
        private void OnDisable()
        {
            if (context == null) return;

            thrustInput = 0f;
            strafeInput = 0f;
            rotationInput = 0f;
            boostInput = false;
            wantsRotate = false;
            primaryHeld = false;
            secondaryHeld = false;

            pilot.Drive(default);
            if (weapons != null)
            {
                FireSlot(WeaponSlot.Primary, false, ref prevPrimaryHeld);
                FireSlot(WeaponSlot.Secondary, false, ref prevSecondaryHeld);
            }
        }

        private void FixedUpdate()
        {
            if (context == null) return;

            var cmd = new PilotCommand
            {
                thrust = thrustInput,
                strafe = strafeInput,
                boost = (boostInput && context.BoostAvailable) ? 1f : 0f,
                yawTorque = useMouseDirection
                    ? (wantsRotate ? GetMouseRotationTorque() : 0f)
                    : rotationInput,
            };
            boostInput = false;
            pilot.Drive(cmd);

            if (weapons != null)
            {
                FireSlot(WeaponSlot.Primary, primaryHeld, ref prevPrimaryHeld);
                FireSlot(WeaponSlot.Secondary, secondaryHeld, ref prevSecondaryHeld);
            }
        }

        // Pushes raw trigger facts — held state and the press edge — every step; the weapon
        // interprets its own firing semantics (auto/semi/charge) in HandleTrigger.
        private void FireSlot(WeaponSlot slot, bool held, ref bool prevHeld)
        {
            var cmd = new WeaponCommand { held = held, pressed = held && !prevHeld };
            prevHeld = held;
            weapons.Fire(slot, cmd);
        }

        private float GetMouseRotationTorque()
        {
            var kin = context.Kinematics;
            return Ships.Movement.ControlUtils.RotationPd(targetAngle, kin.yaw, kin.yawRate, context.MaxYawRate, 4f);
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
