using UnityEngine;

namespace Cameras
{
    [RequireComponent(typeof(ObserverCam))]
    public class CameraController : MonoBehaviour
    {
        [Tooltip("Key used to toggle locking the camera to the player.")]
        [SerializeField] private KeyCode toggleLockKey = KeyCode.C;

        [Tooltip("CameraFollow component to control. Defaults to the one on the same GameObject.")]
        [SerializeField] private ObserverCam observerCam;

        private void Awake()
        {
            if (!observerCam)
            {
                observerCam = GetComponent<ObserverCam>();
            }
        }

        private void Update()
        {
            if (!observerCam) return;
            if (!Input.GetKeyDown(toggleLockKey)) return;
            observerCam.SetLockCameraToSubject(!observerCam.LockCameraToSubject);
        }
    }
} 