using Game;
using UnityEngine;
using Utils;

// Translates player input into commands for the Ship component.
namespace ShipMain.Control
{
    public class Player : MonoBehaviour, ICommandSource
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
    
        // Cached command that will be built every Update and served to the Ship in FixedUpdate.
        private Command cachedCommand;

        public void InitializeCommander(Ship ship)
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
            cachedCommand = cmd;
        }
    
        public bool TryGetCommand(State state, out Command cmd)
        {
            cmd = cachedCommand;
            return true;
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

        public int Priority => 100; // Player input overrides most others


    /*TODO
        private void OnDrawGizmos()
        {
            if (!showMouseGizmos || !Application.isPlaying || !useMouseDirection || !isMouseActive) return;
        
            Vector3 position = transform.position;
        
            // Draw direction to mouse (red - raw direction)
            if (directionToMouse != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Vector3 mouseVector = directionToMouse * mouseGizmoScale;
                Gizmos.DrawRay(position, mouseVector);
                Gizmos.DrawWireSphere(position + mouseVector, 0.1f * mouseGizmoScale);
            }
        
            // Draw projected direction (orange - projected onto plane)
            if (projectedDirection != Vector3.zero)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
                Vector3 projectedVector = projectedDirection * mouseGizmoScale * 0.8f;
                Gizmos.DrawRay(position, projectedVector);
                Gizmos.DrawWireCube(position + projectedVector, Vector3.one * 0.08f * mouseGizmoScale);
            }
        
            // Draw plane normal for reference (blue)
            Gizmos.color = Color.blue;
            Vector3 normalVector = GamePlane.Normal * mouseGizmoScale * 0.6f;
            Gizmos.DrawRay(position, normalVector);
            Gizmos.DrawWireCube(position + normalVector, Vector3.one * 0.05f * mouseGizmoScale);
        
            // Draw plane forward direction (green)
            Gizmos.color = Color.green;
            Vector3 forwardVector = GamePlane.Forward * mouseGizmoScale * 0.7f;
            Gizmos.DrawRay(position, forwardVector);
            Gizmos.DrawWireCube(position + forwardVector, Vector3.one * 0.06f * mouseGizmoScale);
        }*/
    }
} 