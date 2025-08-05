using UnityEngine;
using Utils;

// Translates player input into commands for the Ship component.
namespace ShipMain.Control
{
    public class Player : MonoBehaviour, ICommandSource
    {
        private Ship ship;
        private Camera mainCamera;

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

            // Ensure the player ship is tagged correctly for game-wide lookups (e.g., GameManager death handling)
            if (ship != null)
            {
                ship.gameObject.tag = TagNames.Player;
            }
        }

        private void Start()
        {
            mainCamera = Camera.main;
        }

        // Unity standard frame update – poll input here for maximum responsiveness.
        void Update()
        {
            // Build a new command from the latest input state each rendered frame.
            Command cmd = new Command();

            // Movement inputs
            cmd.Thrust = Input.GetAxis("Vertical");
            cmd.Strafe = Input.GetAxis("Horizontal");
            cmd.Boost = Input.GetButtonDown("Boost") && ship.Movement.BoostAvailable? 1f : 0f;

            // Rotation handling (mouse or axis driven)
            HandleRotationInput(ref cmd);

            // Shooting inputs
            cmd.PrimaryFire   = Input.GetButton("Fire1");
            cmd.SecondaryFire = Input.GetButtonDown("Fire2");

            // Cache for retrieval during the next physics step.
            cachedCommand = cmd;
        }
    
        public bool TryGetCommand(State state, out Command cmd)
        {
            // Simply return the most recently cached command prepared in Update().
            cmd = cachedCommand;
            return true;
        }


        public void HandleRotationInput(ref Command cmd)
        {
            if (useMouseDirection)
            {
                bool wantsToRotate = Input.GetButton("Direction");
                cmd.RotateToTarget = wantsToRotate;

                if (wantsToRotate)
                {
                    Vector3 mouseWorldPos = GetMouseWorldPosition();
                    directionToMouse = (mouseWorldPos - ship.transform.position).normalized;
                    float targetYaw = CalculateYawAngle(directionToMouse);
                    cmd.TargetAngle = targetYaw;
                    isMouseActive = true;
                }
                else
                {
                    cmd.YawTorque = 0f;
                    isMouseActive = false;
                }
            }
            else
            {
                float rotationInput = Input.GetAxis("Rotation");
                cmd.YawTorque = rotationInput;
                cmd.RotateToTarget = false;
                isMouseActive = false;
            }
        }
    
        private Vector3 GetMouseWorldPosition()
        {
            // Optimization: Early-out if mouse direction is not being used
            // This method should only be called when useMouseDirection is true,
            // but this guard protects against misuse
            if (!useMouseDirection)
            {
                return Vector3.zero;
            }

            Vector3 screenMousePos = Input.mousePosition;
        
            if (mainCamera.orthographic)
            {
                screenMousePos.z = mainCamera.WorldToScreenPoint(ship.transform.position).z;
            }
            else
            {
                screenMousePos.z = Vector3.Distance(mainCamera.transform.position, ship.transform.position);
            }
        
            return mainCamera.ScreenToWorldPoint(screenMousePos);
        }
    
        private float CalculateYawAngle(Vector3 direction)
        {
            Vector3 planeNormal = GamePlane.Normal;

            projectedDirection = Vector3.ProjectOnPlane(direction, planeNormal).normalized;
        
            float angle = Vector3.SignedAngle(GamePlane.Forward, projectedDirection, planeNormal);
        
            if (angle < 0) angle += 360f;
        
            return angle;
        }

        public int Priority => 100; // Player input overrides most others


    
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
        }
    }
} 