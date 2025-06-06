using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    public float parallaxSpeed; // Speed at which this layer moves

    private Transform cameraTransform; // Reference to the main camera
    private Vector3 lastCameraPosition; // Last position of the camera

    private void Start()
    {
        cameraTransform = Camera.main.transform;
        lastCameraPosition = cameraTransform.position;
    }

    private void LateUpdate()
    {
        // Calculate the movement of the camera
        Vector3 deltaMovement = cameraTransform.position - lastCameraPosition;

        // Move the background layer based on the camera's movement and parallax speed
        transform.position += new Vector3(deltaMovement.x * parallaxSpeed, deltaMovement.y * parallaxSpeed, 0);

        // Update the last camera position
        lastCameraPosition = cameraTransform.position;
    }
}