using System;
using Combat.Weapons.Arsenal;
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
        [Tooltip("Crosshair colour while one of the ship's wired missiles flies.")]
        [SerializeField] private Color wiredTint = new(0.35f, 0.6f, 1f, 1f);

        private PlayerControls controls;
        private Canvas rootCanvas;
        private RectTransform rect;
        private Graphic[] artwork;
        private Color[] artworkColors;
        private IWireReadout wire;
        private bool wiredShown;
        private IShipStatus ship;
        private Camera gameplayCamera;
        private bool focused;

        private void Awake()
        {
            if (!pointer) throw new InvalidOperationException("AimCrosshair requires its pointer.");
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            rect = (RectTransform)transform;
            artwork = GetComponentsInChildren<Graphic>(true);
            artworkColors = new Color[artwork.Length];
            for (var i = 0; i < artwork.Length; i++)
                artworkColors[i] = artwork[i].color;
            controls = new PlayerControls();
        }

        public void Initialize(IShipStatus status, Camera camera, IWeaponReadouts weapons)
        {
            ship = status ?? throw new ArgumentNullException(nameof(status));
            if (!camera) throw new ArgumentNullException(nameof(camera));
            gameplayCamera = camera;
            wire = null;
            if (weapons == null) return;
            foreach (var slot in weapons.Slots)
            foreach (var readout in weapons.Readouts(slot))
                if (readout is IWireReadout wired) wire = wired;
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
            ShowWired(wire != null && wire.WireLive);
            ScreenPin.Place(rect, rootCanvas, aim);
            var direction = aim - (Vector2)gameplayCamera.WorldToScreenPoint(ship.Transform.position);
            if (direction.sqrMagnitude > .0001f)
                pointer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }

        private void ShowWired(bool wired)
        {
            if (wired == wiredShown) return;
            wiredShown = wired;
            for (var i = 0; i < artwork.Length; i++)
                artwork[i].color = wired
                    ? new Color(wiredTint.r, wiredTint.g, wiredTint.b, artworkColors[i].a)
                    : artworkColors[i];
        }
    }
}