using System;
using Combat.Weapons;
using Ships.Command;
using UnityEngine;
using Substrate;

namespace Game.Player
{
    /// <summary>
    /// Translates player input into piloting and firing commands, pushed to the injected
    /// <see cref="IPilot"/>/<see cref="IWeapons"/> actuators. Reads ship situation through the
    /// narrow <see cref="IShipStatus"/> — it never touches the Ship directly.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class PlayerCommander : Commander
    {
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
        private bool primaryHeld;
        private bool secondaryHeld;
        private bool prevPrimaryHeld;
        private bool prevSecondaryHeld;
        private bool boostInput;
        private bool wantsRotate;
        private Vector3 cursorPoint;

        // Past any weapon's range: with no cursor the shot aims down the nose and the weapon clamps it.
        private const float NoseAimDistance = 1000f;

        private void Update()
        {
            if (context == null) return;

            thrustInput = playerInput.Thrust;
            strafeInput = playerInput.Strafe;
            boostInput |= playerInput.BoostDown;
            primaryHeld = playerInput.PrimaryFire;
            secondaryHeld = playerInput.SecondaryFire;
            wantsRotate = playerInput.WantsToRotate;
            if (hasScreenProjector)
                cursorPoint = playerInput.GetMouseWorldPosition();

            if (wantsRotate && hasScreenProjector)
            {
                directionToMouse = (cursorPoint - context.Transform.position).normalized;
                targetAngle = CalculateYawAngle(directionToMouse);
            }
        }

        private void OnEnable() => playerInput.Enable();

        private void OnDestroy() => playerInput.Dispose();

        // MovementController latches the last pilot command and charge weapons fire on a
        // trigger-up step — release everything this commander drives when it goes silent.
        private void OnDisable()
        {
            playerInput.Disable();
            if (context == null) return;

            thrustInput = 0f;
            strafeInput = 0f;
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
                yawTorque = wantsRotate ? GetMouseRotationTorque() : 0f,
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
            var cmd = new WeaponCommand { held = held, pressed = held && !prevHeld, targetPoint = TargetPoint() };
            prevHeld = held;
            weapons.Fire(slot, cmd);
        }

        private Vector3 TargetPoint() => hasScreenProjector
            ? cursorPoint
            : context.Transform.position + GamePlane.PlaneDirToWorld(context.Kinematics.Forward) * NoseAimDistance;

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
