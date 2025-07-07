using UnityEngine;

/// <summary>
/// Keeps a GameObject's local transform fixed relative to its parent.
/// Attach this to pilot module prefabs (or any child object) to ensure it
/// remains aligned at the origin of the parent ship regardless of manual edits.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class LockLocalTransform : MonoBehaviour
{
    [Tooltip("Desired local position that will be enforced each frame and when edited.")]
    public Vector3 lockedLocalPosition = Vector3.zero;

    [Tooltip("When true, local rotation will be forced to identity (0,0,0).")]
    public bool lockRotation = true;

    [Tooltip("When true, local scale will be forced to (1,1,1).")]
    public bool lockScale = true;

    // Called on enabling and in edit-time validation to enforce the lock.
    private void OnEnable()
    {
        ApplyLock();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure any inspector changes are immediately applied.
        ApplyLock();
    }
#endif

    private void LateUpdate()
    {
        // Runtime enforcement (runs in both Edit and Play modes due to ExecuteAlways).
        ApplyLock();
    }

    private void ApplyLock()
    {
        transform.localPosition = lockedLocalPosition;
        if (lockRotation) transform.localRotation = Quaternion.identity;
        if (lockScale) transform.localScale = Vector3.one;
    }
} 