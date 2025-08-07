using Game;
using UnityEngine;
using Utils;

// Translates player input into commands for the Ship component.
namespace ShipMain.Control
{
    public class Player : Commander
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
            if (ship)
                ship.gameObject.tag = TagNames.Player;
        }

        // Unity standard frame update – poll input here for maximum responsiveness.
        private void Update()
        {
            var (y, t, r) = HandleRotationInput();
            var cmd = new Command
            {
                Thrust = Input.GetAxis("Vertical"),
                Strafe = Input.GetAxis("Horizontal"),
                Boost = Input.GetButtonDown("Boost") && ship.Movement.BoostAvailable? 1f : 0f,
                PrimaryFire   = Input.GetButton("Fire1"),
                SecondaryFire = Input.GetButtonDown("Fire2"),
                YawTorque = y,
                TargetAngle = t,
                RotateToTarget = r
            };
            CachedCommand = cmd;
        }

        private (float, float, bool) HandleRotationInput()
        {
            float yawTorque =0 , targetRot = 0;
            bool isRot;
            if (useMouseDirection)
            {
                bool wantsToRotate = Input.GetButton("Direction");
                isRot = wantsToRotate;

                if (wantsToRotate)
                {
                    var mouseWorldPos = MouseInput.Singleton.GetMouseWorldPosition();
                    directionToMouse = (mouseWorldPos - ship.transform.position).normalized;
                    targetRot = CalculateYawAngle(directionToMouse);
                    isMouseActive = true;
                }
                else
                {
                    isMouseActive = false;
                }
            }
            else
            {
                float rotationInput = Input.GetAxis("Rotation");
                yawTorque = rotationInput;
                isRot = false;
                isMouseActive = false;
            }
            return (yawTorque,  targetRot, isRot);
        }
    
        private float CalculateYawAngle(Vector3 direction)
        {
            var planeNormal = GamePlane.Normal;

            projectedDirection = Vector3.ProjectOnPlane(direction, planeNormal).normalized;
        
            float angle = Vector3.SignedAngle(GamePlane.Forward, projectedDirection, planeNormal);
        
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