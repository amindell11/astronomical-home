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
    private MissileLauncher missileLauncher;

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
        missileLauncher = GetComponentInChildren<MissileLauncher>();
    }

    public void HandleRotationInput(ref ShipCommand cmd)
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
                cmd.TargetAngle = 0f;
                isMouseActive = false;
            }
        }
        else
        {
            float rotationInput = Input.GetAxis("Rotation");
            bool shouldRotate = Mathf.Abs(rotationInput) > 0.2f;
            cmd.RotateToTarget = shouldRotate;
            float currentYaw = shipController.Angle;
            cmd.TargetAngle = currentYaw + (rotationInput * 90f);
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

    public bool TryGetCommand(ShipState state, out ShipCommand cmd)
    {
        cmd = new ShipCommand();
        // Read movement inputs
        cmd.Thrust = Input.GetAxis("Vertical");
        cmd.Strafe = Input.GetAxis("Horizontal");

        HandleRotationInput(ref cmd);

        // Shooting
        cmd.PrimaryFire = Input.GetButton("Fire1");

        // Missile lock / fire (single press behaviour)
        cmd.SecondaryFire = Input.GetButtonDown("Fire2");
        
        // Always provide command; central Ship may decide to ignore if zeroed
        return true;
    }

    public void InitializeCommander(Ship ship)
    {
        // This commander doesn't need any specific initialization from the ship.
    }

    /* ───────────────────────── Missile Target Helper ───────────────────────── */
    ITargetable PickTarget()
    {
        Vector3 origin = missileLauncher && missileLauncher.firePoint ? missileLauncher.firePoint.position : transform.position;
        Vector3 dir    = transform.up; // ship forward in top-down view
        if (Physics.Raycast(origin, dir, out RaycastHit hit, 100f))
        {
            return hit.collider.GetComponentInParent<ITargetable>();
        }
        return null;
    }
} 