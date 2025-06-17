using UnityEngine;

// Translates player input into commands for the Ship component.
[RequireComponent(typeof(ShipMovement))]
public class PlayerShipInput : MonoBehaviour
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
        float thrustInput = Input.GetAxis("Vertical");
        float strafeInput = Input.GetAxis("Horizontal");
        shipController.SetControls(thrustInput, strafeInput);
        HandleRotationInput();

        // Handle shooting input
        if (Input.GetButton("Fire1"))
        {
            laserGun?.Fire();
        }
    }

    public void HandleRotationInput()
    {
        if (useMouseDirection)
        {
            bool wantsToRotate = Input.GetButton("Direction");
            
            if (wantsToRotate)
            {
                Vector3 mouseWorldPos = GetMouseWorldPosition();
                directionToMouse = (mouseWorldPos - ship.transform.position).normalized;
                
                // Calculate angle relative to the ship's reference plane
                float targetYaw = CalculateYawAngle(directionToMouse);
                
                shipController.SetRotationTarget(true, targetYaw);
                isMouseActive = true;
            }
            else
            {
                shipController.SetRotationTarget(false, 0f);
                isMouseActive = false;
            }
        }
        else
        {
            // Direct rotation input
            float rotationInput = Input.GetAxis("Rotation");
            bool shouldRotate = Mathf.Abs(rotationInput) > 0.2f;
            float targetYaw = shipController.Angle + (rotationInput * 90f);
            
            shipController.SetRotationTarget(shouldRotate, targetYaw);
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
        Vector3 planeNormal = ship.GetPlaneNormal();
        
        projectedDirection = Vector3.ProjectOnPlane(direction, planeNormal).normalized;
        
        float angle = Vector3.SignedAngle(ship.GetPlaneForward(), projectedDirection, planeNormal);
        
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
        Vector3 normalVector = ship.GetPlaneNormal() * mouseGizmoScale * 0.6f;
        Gizmos.DrawRay(position, normalVector);
        Gizmos.DrawWireCube(position + normalVector, Vector3.one * 0.05f * mouseGizmoScale);
        
        // Draw plane forward direction (green)
        Gizmos.color = Color.green;
        Vector3 forwardVector = ship.GetPlaneForward() * mouseGizmoScale * 0.7f;
        Gizmos.DrawRay(position, forwardVector);
        Gizmos.DrawWireCube(position + forwardVector, Vector3.one * 0.06f * mouseGizmoScale);
    }
} 