using System;
using Combat.Weapons;
using Ships.Command;
using UnityEngine;
using Substrate;

namespace Game.Player
{
    /// <summary>
    /// Translates player input into piloting, firing and jump commands, pushed to the injected
    /// <see cref="IPilot"/>/<see cref="IWeapons"/>/<see cref="IJumpDrive"/> actuators. Reads ship situation through the
    /// narrow <see cref="IShipStatus"/> — it never touches the Ship directly. While the flight assist is on
    /// (V toggles it), a movement axis with no input counter-thrusts the ship's drift along it on its own engines.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class PlayerCommander : Commander
    {
        [SerializeField] private FlightAssistProfile assist;

        private IShipStatus context;
        private IPilot pilot;
        private IWeapons weapons;
        private IJumpDrive jump;
        internal PlayerInputReader playerInput;
        private bool hasScreenProjector;

        internal Vector3 directionToMouse;
        internal Vector3 projectedDirection;
        private float targetAngle;

        public bool HasScreenProjectorConfigured => hasScreenProjector;

        private void Awake()
        {
            if (!assist) throw new InvalidOperationException($"{name} has no {nameof(FlightAssistProfile)} assigned.");
            playerInput = new PlayerInputReader(_ => throw new InvalidOperationException("Screen-to-game-plane projector has not been configured."));
            playerInput.JumpPressed += RequestJump;
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
            jump = control.Jump;
        }

        private float thrustInput;
        private float strafeInput;
        private bool primaryHeld;
        private bool secondaryHeld;
        private bool prevPrimaryHeld;
        private bool prevSecondaryHeld;
        private bool primaryReload;
        private bool secondaryReload;
        private bool boostInput;
        private bool wantsRotate;
        private Vector3 cursorPoint;
        private bool assistOn = true;
        private float thrustInputAt = float.NegativeInfinity;
        private float strafeInputAt = float.NegativeInfinity;
        private float boostPressedAt = float.NegativeInfinity;

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
            primaryReload |= playerInput.PrimaryReload;
            secondaryReload |= playerInput.SecondaryReload;
            wantsRotate = playerInput.WantsToRotate;
            if (playerInput.AssistTogglePressed) assistOn = !assistOn;
            if (hasScreenProjector)
                cursorPoint = playerInput.GetMouseWorldPosition();

            if (wantsRotate && hasScreenProjector)
            {
                directionToMouse = (cursorPoint - context.Transform.position).normalized;
                targetAngle = CalculateYawAngle(directionToMouse);
            }
        }

        private void RequestJump()
        {
            if (context == null) return;
            jump.RequestJump();
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
            primaryReload = false;
            secondaryReload = false;

            pilot.Drive(default);
            if (weapons != null)
            {
                FireSlot(WeaponSlot.Primary, false, ref prevPrimaryHeld, ref primaryReload);
                FireSlot(WeaponSlot.Secondary, false, ref prevSecondaryHeld, ref secondaryReload);
            }
        }

        private void FixedUpdate()
        {
            if (context == null) return;

            if (thrustInput != 0f) thrustInputAt = Time.time;
            if (strafeInput != 0f) strafeInputAt = Time.time;
            if (boostInput) boostPressedAt = Time.time;

            var forward = context.Kinematics.Forward;
            var cmd = new PilotCommand
            {
                thrust = Assisted(thrustInput, thrustInputAt, forward),
                strafe = Assisted(strafeInput, strafeInputAt, new Vector2(forward.y, -forward.x)),
                boost = (boostInput && context.BoostAvailable) ? 1f : 0f,
                yawTorque = wantsRotate ? GetMouseRotationTorque() : 0f,
                abilityTarget = hasScreenProjector ? (Vector2?)GamePlane.WorldPointToPlane(cursorPoint) : null,
            };
            boostInput = false;
            pilot.Drive(cmd);

            if (weapons != null)
            {
                FireSlot(WeaponSlot.Primary, primaryHeld, ref prevPrimaryHeld, ref primaryReload);
                FireSlot(WeaponSlot.Secondary, secondaryHeld, ref prevSecondaryHeld, ref secondaryReload);
            }
        }

        private float Assisted(float input, float inputAt, Vector2 axis)
        {
            if (!assistOn || input != 0f) return input;
            if (Time.time - Mathf.Max(inputAt, boostPressedAt) < assist.delay) return 0f;
            var drift = Vector2.Dot(context.Kinematics.vel, axis);
            return -Mathf.Clamp(drift / assist.fullBrakeSpeed, -1f, 1f) * assist.strength;
        }

        // Raw input facts every step; the weapon interprets its own firing semantics in HandleTrigger.
        private void FireSlot(WeaponSlot slot, bool held, ref bool prevHeld, ref bool reload)
        {
            var cmd = new WeaponCommand { held = held, pressed = held && !prevHeld, reload = reload, targetPoint = TargetPoint() };
            prevHeld = held;
            reload = false;
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
