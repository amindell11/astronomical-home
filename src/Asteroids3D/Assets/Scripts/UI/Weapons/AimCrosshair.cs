using System;
using Game.Player;
using Ships.Command;
using UnityEngine;

namespace UI.Weapons
{
    public sealed class AimCrosshair : MonoBehaviour
    {
        [SerializeField] private RectTransform pointer;

        private PlayerControls controls;
        private Canvas rootCanvas;
        private RectTransform rect;
        private RectTransform parentRect;
        private IShipStatus ship;
        private Camera gameplayCamera;
        private bool previousCursorVisible;

        private void Awake()
        {
            if (!pointer) throw new InvalidOperationException("AimCrosshair requires its pointer.");
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            rect = (RectTransform)transform;
            parentRect = (RectTransform)transform.parent;
            controls = new PlayerControls();
        }

        public void Initialize(IShipStatus status, Camera camera)
        {
            ship = status ?? throw new ArgumentNullException(nameof(status));
            if (!camera) throw new ArgumentNullException(nameof(camera));
            gameplayCamera = camera;
        }

        private void OnEnable()
        {
            controls.Flight.Enable();
            previousCursorVisible = Cursor.visible;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            controls.Flight.Disable();
            Cursor.visible = previousCursorVisible;
        }

        private void OnDestroy() => controls?.Dispose();

        private void LateUpdate()
        {
            var aim = controls.Flight.AimPoint.ReadValue<Vector2>();
            var uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, aim, uiCamera, out var point);
            rect.localPosition = point;
            var direction = aim - (Vector2)gameplayCamera.WorldToScreenPoint(ship.Transform.position);
            if (direction.sqrMagnitude > .0001f)
                pointer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }
    }
}