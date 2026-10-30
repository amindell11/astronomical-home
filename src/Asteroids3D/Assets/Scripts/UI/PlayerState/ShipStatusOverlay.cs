using System.Collections.Generic;
using Ships;
using UnityEngine;
using Utils;

namespace UI.PlayerState
{
    /// <summary>
    /// Session-owned ship indicators. A separate canvas preserves their recap fade independently of the main HUD.
    /// </summary>
    public sealed class ShipStatusOverlay : MonoBehaviour
    {
        private readonly Dictionary<Ship, ShipStatusIndicator> indicators = new();
        private SubscribedSet<Ship> ships;
        private Camera worldCamera;
        private Canvas canvas;
        private Font font;
        private Material textMaterial;
        private CanvasGroup visibility;

        public static ShipStatusOverlay Create(Canvas hud, SubscribedSet<Ship> ships, Camera worldCamera, Font font, Material textMaterial)
        {
            var root = new GameObject("Ship status overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.layer = hud.gameObject.layer;
            root.transform.SetParent(hud.transform.parent, false);
            var overlay = root.AddComponent<ShipStatusOverlay>();
            overlay.canvas.renderMode = RenderMode.ScreenSpaceCamera;
            overlay.canvas.worldCamera = hud.worldCamera;
            overlay.canvas.planeDistance = hud.worldCamera.nearClipPlane + .1f;
            overlay.canvas.sortingOrder = hud.sortingOrder - 1;
            overlay.worldCamera = worldCamera;
            overlay.textMaterial = textMaterial;
            overlay.font = font;
            overlay.ships = ships;
            ships.OnAdd += overlay.Add;
            ships.OnRemove += overlay.Remove;
            foreach (var ship in ships) overlay.Add(ship);
            return overlay;
        }

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            visibility = GetComponent<CanvasGroup>();
        }

        public void SetOpacity(float opacity) => visibility.alpha = opacity;

        private void Add(Ship ship) => indicators.Add(ship,
            ShipStatusIndicator.Create(canvas, worldCamera, ship, font, textMaterial));

        private void Remove(Ship ship)
        {
            Destroy(indicators[ship].gameObject);
            indicators.Remove(ship);
        }

        private void OnDestroy()
        {
            if (ships == null) return;
            ships.OnAdd -= Add;
            ships.OnRemove -= Remove;
        }
    }
}
