using UnityEngine;

/// <summary>
/// This component stabilizes the GameObject it's attached to, ensuring its rotation
/// only reflects the parent's rotation around the GamePlane's normal axis.
/// It ignores pitch and roll relative to the GamePlane.
/// This is useful for attaching a RayPerceptionSensor3D to a child object of a ship
/// to ensure that its raycasts are always parallel to the GamePlane.
/// </summary>
public class RaycastStabilizer : MonoBehaviour
{
    private Transform parentTransform;
    private bool gamePlaneAvailable;

    void Awake()
    {
        // This component must be on a child object.
        parentTransform = transform.parent;
        if (parentTransform == null)
        {
            Debug.LogError("RaycastStabilizer must be placed on a child GameObject.", this);
            enabled = false;
            return;
        }
        
        gamePlaneAvailable = GamePlane.Plane != null;
        if (!gamePlaneAvailable)
        {
            Debug.LogWarning("GamePlane not found. RaycastStabilizer will fall back to world's XZ plane stabilization.", this);
        }
    }

    void LateUpdate()
    {
        if (parentTransform == null) return;

        // Match the parent's position exactly.
        transform.position = parentTransform.position;

        if (gamePlaneAvailable)
        {
            // Get the normal of the game plane, which will be our 'up' vector.
            Vector3 planeNormal = GamePlane.Normal;

            // Project the parent's forward vector onto the game plane.
            // This effectively removes any rotation component not aligned with the plane normal (e.g., pitch/roll relative to the plane).
            Vector3 projectedForward = Vector3.ProjectOnPlane(parentTransform.forward, planeNormal);
            
            // We can only create a valid rotation if the projected forward vector is not zero.
            // This would happen if the parent's forward is parallel to the plane normal.
            if (projectedForward.sqrMagnitude > 1e-6f)
            {
                // Create a new rotation that looks along the projected forward direction,
                // with 'up' aligned to the plane's normal.
                transform.rotation = Quaternion.LookRotation(projectedForward, planeNormal);
            }
            // else: Edge case where parent is looking along the plane normal. We don't update
            // the rotation to prevent unpredictable spinning.
        }
        else
        {
            // Fallback to original behavior if GamePlane is not set up, assuming world's XZ plane.
            float parentYaw = parentTransform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0, parentYaw, 0);
        }
    }
} 