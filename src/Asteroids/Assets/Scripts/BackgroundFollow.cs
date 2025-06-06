using UnityEngine;

public class BackgroundFollow : MonoBehaviour
{
    private Transform cameraTransform;
    private Vector3 lastCameraPosition;
    private Vector3 offset;

    private void Start()
    {
        // Get the main camera's transform
        cameraTransform = Camera.main.transform;
        
        // Store initial offset between background and camera
        offset = transform.position - cameraTransform.position;
        
        // Store initial camera position
        lastCameraPosition = cameraTransform.position;
    }

    private void LateUpdate()
    {
        // Calculate the camera's movement delta
        Vector3 cameraDelta = cameraTransform.position - lastCameraPosition;
        
        // Move the background by the same amount
        transform.position += cameraDelta;
        
        // Make the background follow the camera's rotation
        transform.rotation = cameraTransform.rotation;
        
        // Update last camera position
        lastCameraPosition = cameraTransform.position;
    }
} 