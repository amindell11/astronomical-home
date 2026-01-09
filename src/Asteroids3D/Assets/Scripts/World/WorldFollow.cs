using Editor;
using Game;
using UnityEngine;
using UnityEngine.Serialization;

public class WorldFollow : MonoBehaviour
{
    [FormerlySerializedAs("Target")] [SerializeField] public Transform target;        // The player to follow
    [SerializeField] private Vector3 offset;         // Offset from the target
    [SerializeField] private float smoothSpeed = 5f; // How smoothly the camera follows
    
    private void FixedUpdate()
    {
        if (!target) return;

        // Calculate the desired position
        var desiredPosition = target.position + offset;
        
        // Smoothly move the camera using fixedDeltaTime
        var smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.fixedDeltaTime);
        transform.position = smoothedPosition;
    }

    // Optional: Method to change the target at runtime
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // Optional: Method to update the offset at runtime
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
} 