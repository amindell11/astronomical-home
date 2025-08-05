using UnityEngine;

namespace EnemyAI.RL
{
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
        // Local-space rotation captured at startup so that any design-time tweaks are preserved.
        private Quaternion initialLocalRotation;

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
        
            // Remember the initial rotation relative to the parent so that we can re-apply it
            // after constraining the up-vector to the GamePlane normal each frame.
            initialLocalRotation = transform.localRotation;

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

            // Step 1 — Determine which "up" vector to use (GamePlane normal or world-up fallback).
            Vector3 planeNormal = gamePlaneAvailable ? GamePlane.Normal : Vector3.up;

            // Step 2 — Start from the parent's current rotation plus whatever local offset the
            // designer put on this object in the editor.
            Quaternion rawRotation = parentTransform.rotation * initialLocalRotation;

            // Step 3 — Constrain the object so that its local Y-axis aligns with the plane normal.
            // We achieve this by projecting the desired forward vector onto the plane and then
            // building a rotation that uses the plane normal as the up direction.
            Vector3 desiredForward = rawRotation * Vector3.forward;
            Vector3 projectedForward = Vector3.ProjectOnPlane(desiredForward, planeNormal);

            // If the forward vector is (nearly) parallel to the normal, fall back to the right axis
            // to avoid zero-length vectors that would produce NaNs.
            if (projectedForward.sqrMagnitude < 1e-6f)
            {
                projectedForward = Vector3.ProjectOnPlane(rawRotation * Vector3.right, planeNormal);
            }

            // Final stabilized rotation.
            transform.rotation = Quaternion.LookRotation(projectedForward, planeNormal);
        }
    }
} 