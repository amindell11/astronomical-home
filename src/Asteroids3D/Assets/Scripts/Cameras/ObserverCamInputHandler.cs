using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cameras
{
    [RequireComponent(typeof(ObserverCam))]
    public class ObserverCamInputHandler : MonoBehaviour
    {
        [Tooltip("CameraFollow component to control. Defaults to the one on the same GameObject.")]
        [SerializeField] private ObserverCam observerCam;

        private PlayerControls controls;
        private InputAction cycleView;

        private void Awake()
        {
            if (!observerCam)
                observerCam = GetComponent<ObserverCam>();
            controls = new PlayerControls();
            cycleView = new InputAction("Cycle flight view", InputActionType.Button, "<Keyboard>/f6");
        }

        private void OnEnable()
        {
            controls.Camera.Enable();
            cycleView.Enable();
        }

        private void OnDisable()
        {
            controls.Camera.Disable();
            cycleView.Disable();
        }

        private void OnDestroy()
        {
            controls.Dispose();
            cycleView.Dispose();
        }

        private void Update()
        {
            if (!observerCam) return;
            if (observerCam.Cam.enabled && cycleView.WasPressedThisFrame()) observerCam.CycleView();
            if (!controls.Camera.ToggleLock.WasPressedThisFrame()) return;
            observerCam.SetLockCameraToSubject(!observerCam.LockCameraToSubject);
        }

        private void OnGUI()
        {
            if (!observerCam || !observerCam.Cam.enabled) return;
            var label = observerCam.View switch
            {
                FlightView.Original => "Original",
                FlightView.TiltedOrthographic => "Tilted ortho",
                _ => "Tilted perspective"
            };
            GUI.Box(new Rect(Screen.width * 0.5f - 125f, Screen.height - 38f, 250f, 26f), $"F6  Camera: {label}");
        }
    }
}
