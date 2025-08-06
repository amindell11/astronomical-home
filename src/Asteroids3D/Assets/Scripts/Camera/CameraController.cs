using UnityEngine;

[RequireComponent(typeof(CameraFollow))]
public class CameraController : MonoBehaviour
{
    [Tooltip("Key used to toggle locking the camera to the player.")]
    [SerializeField] private KeyCode toggleLockKey = KeyCode.C;

    [Tooltip("CameraFollow component to control. Defaults to the one on the same GameObject.")]
    [SerializeField] private CameraFollow cameraFollow;

    private void Awake()
    {
        if (cameraFollow == null)
        {
            cameraFollow = GetComponent<CameraFollow>();
        }

        if (cameraFollow == null)
        {
            Debug.LogWarning("CameraController could not find a CameraFollow component to control.");
        }
    }

    private void Update()
    {
        if (cameraFollow == null) return;

        // Toggle lock when the key is pressed
        if (Input.GetKeyDown(toggleLockKey))
        {
            bool newState = !cameraFollow.LockCameraToPlayer;
            cameraFollow.SetLockCameraToPlayer(newState);
        }
    }
} 