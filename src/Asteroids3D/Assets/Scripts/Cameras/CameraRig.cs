using UnityEngine;

namespace Cameras
{
    /// <summary>
    /// Holds references to cameras in a camera rig hierarchy.
    /// Attach to the root camera object and wire up child cameras in the inspector.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Camera uiCamera;
        public ObserverCam ObserverCam { get; private set; }

        private void Awake()
        {
            ObserverCam = GetComponent<ObserverCam>();
        }
    
        public UnityEngine.Camera MainCamera => mainCamera;
        public UnityEngine.Camera UICamera => uiCamera;
    }
}

