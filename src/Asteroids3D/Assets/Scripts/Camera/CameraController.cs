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
        if (!cameraFollow)
        {
            cameraFollow = GetComponent<CameraFollow>();
        }
        if (!cameraFollow)
        {
            Debug.LogWarning("CameraController could not find a CameraFollow component to control.");
        }
    }

    private void Update()
    {
        if (!cameraFollow) return;
        if (!Input.GetKeyDown(toggleLockKey)) return;
        cameraFollow.SetLockCameraToSubject(!cameraFollow.LockCameraToSubject);
    }
} 