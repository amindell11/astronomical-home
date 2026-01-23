using Game;
using Ships.Command;
using UnityEngine;
using Utils;

// Translates player input into commands for the Ship component.
namespace Ships.Control
{
    public class PlayerCommander : Commander
    {
        private Ship ship;

        [Tooltip("If checked, the ship will rotate towards the mouse position. If unchecked, the ship will rotate using the rotation input axis.")]
        public bool useMouseDirection = false;
    
        [Header("Gizmo Settings")]
        public bool showMouseGizmos = true;
        public float mouseGizmoScale = 3f;
    
        // Gizmo visualization data
        private Vector3 directionToMouse;
        private Vector3 projectedDirection;
        private bool isMouseActive;
        
        public override void InitializeCommander(Ship ship)
        {
            this.ship = ship;
        }

        // Unity standard frame update – poll input here for maximum responsiveness.
        private void Update()
        {
            if (!ship) return;
            var yawTorque = HandleRotationInput();
            var cmd = new Command.Command
            {
                thrust = Input.GetAxis("Vertical"),
                strafe = Input.GetAxis("Horizontal"),
                boost = Input.GetButtonDown("Boost") && ship.Movement.BoostAvailable? 1f : 0f,
                primaryFire   = Input.GetButton("Fire1"),
                secondaryFire = Input.GetButtonDown("Fire2"),
                yawTorque = yawTorque,
            };
            cachedCommand = cmd;
        }

        private float HandleRotationInput()
        {
            float yawTorque = 0;
            if (useMouseDirection)
            {
                var wantsToRotate = Input.GetButton("Direction");

                if (wantsToRotate)
                {
                    var mouseWorldPos = MouseInput.Singleton.GetMouseWorldPosition();
                    directionToMouse = (mouseWorldPos - ship.transform.position).normalized;
                    var targetRot = CalculateYawAngle(directionToMouse);
                    
                    var kin = ship.Movement.Kinematics;
                    yawTorque = Movement.ControlUtils.RotationPd(targetRot, kin.yaw, kin.yawRate, ship.settings.maxYawRate, 2f);
                    
                    isMouseActive = true;
                }
                else
                {
                    isMouseActive = false;
                }
            }
            else
            {
                yawTorque = Input.GetAxis("Rotation");
                isMouseActive = false;
            }
            return yawTorque;
        }
    
        private float CalculateYawAngle(Vector3 direction)
        {
            var planeNormal = GamePlane.Normal;

            projectedDirection = Vector3.ProjectOnPlane(direction, planeNormal).normalized;
        
            var angle = Vector3.SignedAngle(GamePlane.Forward, projectedDirection, planeNormal);
        
            if (angle < 0) angle += 360f;
        
            return angle;
        }
        
        private void OnDrawGizmos()
        {
            if (!showMouseGizmos || !Application.isPlaying || !useMouseDirection || !isMouseActive) return;
        
            var position = transform.position;
            var scale = mouseGizmoScale;

            SuperGizmos.DrawArrow(position, directionToMouse, 
                SuperGizmos.HeadType.Sphere, 0.1f * scale, Color.red, scale);
        
            SuperGizmos.DrawArrow(position, projectedDirection, 
                SuperGizmos.HeadType.Cube, 0.08f * scale, Color.orange, scale);
        
            SuperGizmos.DrawArrow(position, GamePlane.Normal, 
                SuperGizmos.HeadType.Cube, 0.05f * scale, Color.blue, scale);
        
            SuperGizmos.DrawArrow(position, GamePlane.Forward, 
                SuperGizmos.HeadType.Cube, 0.06f * scale, Color.green, scale);
        }
    }
} 