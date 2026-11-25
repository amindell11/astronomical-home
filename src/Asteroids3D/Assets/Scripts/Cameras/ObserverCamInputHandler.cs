using Game.Player;
using UnityEngine;

namespace Cameras
{
    [RequireComponent(typeof(ObserverCam))]
    public class ObserverCamInputHandler : MonoBehaviour
    {
        [Tooltip("CameraFollow component to control. Defaults to the one on the same GameObject.")]
        [SerializeField] private ObserverCam observerCam;

        private PlayerControls controls;

        private void Awake()
        {
            if (!observerCam)
                observerCam = GetComponent<ObserverCam>();
            controls = new PlayerControls();
        }

        private void OnEnable() => controls.Camera.Enable();

        private void OnDisable() => controls.Camera.Disable();

        private void OnDestroy() => controls.Dispose();

        private void Update()
        {
            if (!observerCam) return;
            var scroll = controls.Camera.Zoom.ReadValue<float>();
            if (scroll != 0f && CameraSettings.ManualCamera)
                observerCam.StepZoom(scroll);
            var point = controls.Camera.Point;
            observerCam.SetCursor(point.IsInProgress() ? point.ReadValue<Vector2>() : null);
        }
    }
}
