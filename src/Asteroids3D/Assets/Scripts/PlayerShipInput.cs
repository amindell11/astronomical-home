using UnityEngine;

// Translates player input into commands for the Ship component.
[RequireComponent(typeof(Ship))]
public class PlayerShipInput : MonoBehaviour
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
    
    private void Start()
    {
        ship = GetComponent<Ship>();
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
        if(useMouseDirection){
            // Read rotation input
            bool wantsToRotate = Input.GetButton("Direction");
            Debug.Log($"Mouse rotation - Wants to rotate: {wantsToRotate}");
            
            Vector3 mousePos = Vector3.zero;
            if (wantsToRotate)
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
                
                mousePos = mainCamera.ScreenToWorldPoint(screenMousePos);
                Debug.Log($"Camera orthographic: {mainCamera.orthographic}, Screen mouse pos: {screenMousePos}, World mouse pos: {mousePos}");
                
                directionToMouse = (mousePos - ship.transform.position).normalized;
                Debug.Log($"Direction to mouse: {directionToMouse}");
                Debug.Log($"Ship forward (up): {ship.transform.up}");
                
                // Calculate angle in the ship's local yaw plane (around forward axis)
                projectedDirection = Vector3.ProjectOnPlane(directionToMouse, ship.transform.forward);
                float angle = Vector3.SignedAngle(ship.transform.up, projectedDirection, ship.transform.forward);
                Debug.Log($"Projected direction: {projectedDirection}, Calculated angle: {angle}");
                
                ship.SetRotationTargetAngle(wantsToRotate, angle);
                isMouseActive = true;
            }
            else
            {
                ship.SetRotationTargetAngle(false, 0f);
                isMouseActive = false;
            }
        }else{
            float rotationInput = Input.GetAxis("Rotation");
            Debug.Log($"Direct rotation input: {rotationInput}");
            ship.SetRotationTargetAngle(Mathf.Abs(rotationInput)>.2f, rotationInput * 90f);
            isMouseActive = false;
        }
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
        
        // Draw projected direction (orange - projected onto yaw plane)
        if (projectedDirection != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
            Vector3 projectedVector = projectedDirection * mouseGizmoScale * 0.8f;
            Gizmos.DrawRay(position, projectedVector);
            Gizmos.DrawWireCube(position + projectedVector, Vector3.one * 0.08f * mouseGizmoScale);
        }
        
        // Draw ship's forward direction for reference (blue)
        Gizmos.color = Color.blue;
        Vector3 forwardVector = transform.forward * mouseGizmoScale * 0.6f;
        Gizmos.DrawRay(position, forwardVector);
        Gizmos.DrawWireCube(position + forwardVector, Vector3.one * 0.05f * mouseGizmoScale);
    }
}