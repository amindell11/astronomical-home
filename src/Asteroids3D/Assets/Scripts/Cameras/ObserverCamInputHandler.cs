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

        // A turning view would sweep the world under a still cursor and the ship would chase its own aim.
        private void LateUpdate()
        {
            var mouse = Mouse.current;
            var subject = observerCam ? observerCam.Subject : null;
            if (mouse == null || !subject || observerCam.ViewTurn == 0f || Time.timeScale <= 0f || !Application.isFocused)
                return;
            var cursor = mouse.position.ReadValue();
            if (!new Rect(0f, 0f, Screen.width, Screen.height).Contains(cursor)) return;
            var pivot = (Vector2)observerCam.Cam.WorldToScreenPoint(subject.position);
            mouse.WarpCursorPosition(CameraUtils.RotateAbout(cursor, pivot, -observerCam.ViewTurn));
        }
    }
}
