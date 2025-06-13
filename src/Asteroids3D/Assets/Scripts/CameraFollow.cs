using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;        // The player to follow
    [SerializeField] private Vector3 offset;         // Offset from the target
    [SerializeField] private float smoothSpeed = 5f; // How smoothly the camera follows

    private void Start()
    {
        // If no target is assigned, try to find the player
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("No target assigned to CameraFollow and no Player found!");
            }
        }

        // If no offset is set, use the current position difference
        if (offset == Vector3.zero && target != null)
        {
            offset = transform.position - target.position;
        }
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        // Calculate the desired position
        Vector3 desiredPosition = target.position + offset;
        
        // Smoothly move the camera using fixedDeltaTime
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.fixedDeltaTime);
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