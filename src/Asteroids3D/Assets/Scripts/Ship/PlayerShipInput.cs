using UnityEngine;
using ShipControl;

// Translates player input into commands for the Ship component.
[RequireComponent(typeof(ShipMovement))]
public class PlayerShipInput : MonoBehaviour, IShipCommandSource
{
    private ShipMovement ship;
    private ShipMovement.ShipMovement2D shipController;
    private Camera mainCamera;
    private LaserGun laserGun;

    [Tooltip("If checked, the ship will rotate towards the mouse position. If unchecked, the ship will rotate using the rotation input axis.")]
    public bool useMouseDirection = false;
    
    [Header("Gizmo Settings")]
    public bool showMouseGizmos = true;
    public float mouseGizmoScale = 3f;
    
    // Gizmo visualization data
    private Vector3 directionToMouse;
    private Vector3 projectedDirection;
    private bool isMouseActive;
    
    private ShipCommand _cmd;

    private void Start()
    {
        ship = GetComponent<ShipMovement>();
        shipController = ship.Controller;
        mainCamera = Camera.main;
        laserGun = GetComponentInChildren<LaserGun>();
    }

    private void Update()
    {
        // Read movement inputs
        _cmd.Thrust = Input.GetAxis("Vertical");
        _cmd.Strafe = Input.GetAxis("Horizontal");

        HandleRotationInput();

        // Shooting
        _cmd.Fire = Input.GetButton("Fire1");
    }

    public void HandleRotationInput()
    {
        if (useMouseDirection)
        {
            bool wantsToRotate = Input.GetButton("Direction");
            _cmd.RotateToTarget = wantsToRotate;

            if (wantsToRotate)
            {
                Vector3 mouseWorldPos = GetMouseWorldPosition();
                directionToMouse = (mouseWorldPos - ship.transform.position).normalized;
                float targetYaw = CalculateYawAngle(directionToMouse);
                _cmd.TargetAngle = targetYaw;
                isMouseActive = true;
            }
            else
            {
                _cmd.TargetAngle = 0f;
                isMouseActive = false;
            }
        }
        else
        {
            float rotationInput = Input.GetAxis("Rotation");
            bool shouldRotate = Mathf.Abs(rotationInput) > 0.2f;
            _cmd.RotateToTarget = shouldRotate;
            float currentYaw = shipController.Angle;
            _cmd.TargetAngle = currentYaw + (rotationInput * 90f);
            isMouseActive = false;
        }
    }
    
    private Vector3 GetMouseWorldPosition()
    {
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

    public int Priority => 100; // Player input overrides most others

    public bool TryGetCommand(out ShipCommand cmd)
    {
        cmd = _cmd;
        // Always provide command; central Ship may decide to ignore if zeroed
        return true;
    }
} 