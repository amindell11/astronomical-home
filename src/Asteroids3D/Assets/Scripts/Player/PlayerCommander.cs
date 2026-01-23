using Game;
using Ships;
using Ships.Command;
using UnityEngine;
using Utils;

namespace Player
{
    /// <summary>
    /// Translates player input into commands for the Ship component.
    /// </summary>
    public partial class PlayerCommander : Commander
    {
        [Header("Settings")]
        [Tooltip("If checked, the ship will rotate towards the mouse position. If unchecked, the ship will rotate using the rotation input axis.")]
        [SerializeField] private bool useMouseDirection = false;
    
        [Header("Gizmo Settings")]
        [SerializeField] private bool showMouseGizmos = true;
        [SerializeField] private float mouseGizmoScale = 3f;
    
        private Ship ship;

        private Vector3 directionToMouse;
        private Vector3 projectedDirection;
        private float targetAngle;
        
        public override void InitializeCommander(Ship ship)
        {
            this.ship = ship;
        }

        private void Update()
        {
            if (!ship) return;
            
            // Poll non-physics inputs
            cachedCommand.thrust = PlayerInputReader.Thrust;
            cachedCommand.strafe = PlayerInputReader.Strafe;
            cachedCommand.boost = (PlayerInputReader.BoostDown && ship.Movement.BoostAvailable) ? 1f : 0f;
            cachedCommand.primaryFire   = PlayerInputReader.PrimaryFire;
            cachedCommand.secondaryFire = PlayerInputReader.SecondaryFireDown;

            if (useMouseDirection && PlayerInputReader.WantsToRotate)
            {
                var mouseWorldPos = PlayerInputReader.GetMouseWorldPosition();
                directionToMouse = (mouseWorldPos - ship.transform.position).normalized;
                targetAngle = CalculateYawAngle(directionToMouse);
            }
        }

        private void FixedUpdate()
        {
            if (!ship) return;

            cachedCommand.yawTorque = useMouseDirection 
                ? (PlayerInputReader.WantsToRotate ? GetMouseRotationTorque() : 0)
                : PlayerInputReader.Rotation;
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
 