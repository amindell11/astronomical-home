using System;
using Game.Player;
using Ships.Command;
using UI.Placement;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Weapons
{
    public sealed class AimCrosshair : MonoBehaviour
    {
        [SerializeField] private RectTransform pointer;

        private PlayerControls controls;
        private Canvas rootCanvas;
        private RectTransform rect;
        private Graphic[] artwork;
        private IShipStatus ship;
        private Camera gameplayCamera;
        private bool focused;

        private void Awake()
        {
            if (!pointer) throw new InvalidOperationException("AimCrosshair requires its pointer.");
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            rect = (RectTransform)transform;
            artwork = GetComponentsInChildren<Graphic>(true);
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
            controls.Flight.AimPoint.Enable();
            focused = Application.isFocused;
        }

        private void OnDisable()
        {
            controls.Flight.AimPoint.Disable();
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool hasFocus) => focused = hasFocus;

        private void OnDestroy() => controls?.Dispose();

        private void LateUpdate()
        {
            var aim = controls.Flight.AimPoint.ReadValue<Vector2>();
            // A focused editor Game view keeps the cursor hidden over the other panels too.
            var overGame = focused && new Rect(0f, 0f, Screen.width, Screen.height).Contains(aim);
            Cursor.visible = !overGame;
            foreach (var graphic in artwork)
                graphic.enabled = overGame;
            ScreenPin.Place(rect, rootCanvas, aim);
            var direction = aim - (Vector2)gameplayCamera.WorldToScreenPoint(ship.Transform.position);
            if (direction.sqrMagnitude > .0001f)
                pointer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }
    }
}