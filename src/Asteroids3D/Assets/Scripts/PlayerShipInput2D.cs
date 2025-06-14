using UnityEngine;

// Translates player input into commands for the Ship2D component.
[RequireComponent(typeof(Ship2D))]
public class PlayerShipInput2D : MonoBehaviour
{
    private Ship2D ship;
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
    
    private void Start()
    {
        ship = GetComponent<Ship2D>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // Read movement inputs
        float thrustInput = Input.GetAxis("Vertical");
        float strafeInput = Input.GetAxis("Horizontal");
        ship.SetControls(thrustInput, strafeInput);
        HandleRotationInput();
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
                
                ship.SetRotationTarget(true, targetYaw);
                isMouseActive = true;
                
                Debug.Log($"Mouse input - World pos: {mouseWorldPos}, Direction: {directionToMouse}, Target yaw: {targetYaw}");
            }
            else
            {
                ship.SetRotationTarget(false, 0f);
                isMouseActive = false;
            }
        }
        else
        {
            // Direct rotation input
            float rotationInput = Input.GetAxis("Rotation");
            bool shouldRotate = Mathf.Abs(rotationInput) > 0.2f;
            float targetYaw = ship.CurrentYaw + (rotationInput * 90f); // Relative to current yaw
            
            ship.SetRotationTarget(shouldRotate, targetYaw);
            isMouseActive = false;
            
            if (shouldRotate)
                Debug.Log($"Direct rotation input: {rotationInput}, Target yaw: {targetYaw}");
        }
    }
    
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 screenMousePos = Input.mousePosition;
        
        if (mainCamera.orthographic)
        {
            // For orthographic cameras, project mouse onto the ship's plane
            screenMousePos.z = mainCamera.WorldToScreenPoint(ship.transform.position).z;
        }
        else
        {
            // For perspective cameras, use distance from camera to ship
            screenMousePos.z = Vector3.Distance(mainCamera.transform.position, ship.transform.position);
        }
        
        return mainCamera.ScreenToWorldPoint(screenMousePos);
    }
    
    private float CalculateYawAngle(Vector3 direction)
    {
        // Get the reference plane vectors
        Vector3 planeForward = GetPlaneForward();
        Vector3 planeRight = GetPlaneRight();
        Vector3 planeNormal = GetPlaneNormal();
        
        // Project the direction onto the reference plane
        projectedDirection = Vector3.ProjectOnPlane(direction, planeNormal).normalized;
        
        // Calculate angle from the plane's forward direction
        float angle = Vector3.SignedAngle(planeForward, projectedDirection, planeNormal);
        
        // Convert to 0-360 range
        if (angle < 0) angle += 360f;
        
        return angle;
    }
    
    // Helper methods to get plane directions (matching Ship2D)
    private Vector3 GetPlaneForward()
    {
        if (ship.referencePlane != null)
            return ship.referencePlane.up;
        else
            return Vector3.up; // World Y is forward
    }

    private Vector3 GetPlaneRight()
    {
        if (ship.referencePlane != null)
            return ship.referencePlane.right;
        else
            return Vector3.right; // World X is right
    }

    private Vector3 GetPlaneNormal()
    {
        if (ship.referencePlane != null)
            return ship.referencePlane.forward;
        else
            return Vector3.forward; // World Z is normal to XY plane
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
        Vector3 normalVector = GetPlaneNormal() * mouseGizmoScale * 0.6f;
        Gizmos.DrawRay(position, normalVector);
        Gizmos.DrawWireCube(position + normalVector, Vector3.one * 0.05f * mouseGizmoScale);
        
        // Draw plane forward direction (green)
        Gizmos.color = Color.green;
        Vector3 forwardVector = GetPlaneForward() * mouseGizmoScale * 0.7f;
        Gizmos.DrawRay(position, forwardVector);
        Gizmos.DrawWireCube(position + forwardVector, Vector3.one * 0.06f * mouseGizmoScale);
    }
} 