using UnityEngine;

namespace Cameras
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        public Camera Cam { get; private set; }

        protected virtual void Awake()
        {
            Cam = GetComponent<Camera>();
        }
    }
}
