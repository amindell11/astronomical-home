using System;
using System.Collections.Generic;
using Game.Player;
using Ships;
using Substrate;
using UnityEngine;
using Utils;
using UI.Projection;
using UI.Weapons;

namespace UI.PlayerState
{
    /// <summary>
    /// Session-owned ship indicators and lock reticles share a recap fade independently of the main HUD.
    /// Each frame it picks the player's one target (the missile lock, else the enemy nearest the cursor)
    /// and tells each indicator whether it is targeted.
    /// </summary>
    public sealed class ShipStatusOverlay : MonoBehaviour
    {
        private readonly Dictionary<Ship, ShipStatusIndicator> indicators = new();
        private readonly Dictionary<Ship, InstrumentAppear> reticles = new();
        private SubscribedSet<Ship> ships;
        private Camera worldCamera;
        private Canvas canvas;
        private Font font;
        private Material textMaterial;
        private CanvasGroup visibility;
        private CanvasGroup worldVisibility;
        private LockOnIndicator reticlePrefab;
        private CanopyProjection projection;
        private PlayerControls controls;

        public static ShipStatusOverlay Create(Canvas hud, SubscribedSet<Ship> ships, Camera worldCamera, Font font, Material textMaterial,
            LockOnIndicator reticlePrefab, CanopyProjection projection)
        {
            if (!reticlePrefab) throw new ArgumentNullException(nameof(reticlePrefab));
            if (!projection) throw new ArgumentNullException(nameof(projection));
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
            overlay.reticlePrefab = reticlePrefab;
            overlay.projection = projection;
            overlay.worldVisibility = new GameObject("Ship lock overlay").AddComponent<CanvasGroup>();
            overlay.worldVisibility.gameObject.layer = hud.gameObject.layer;
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
            controls = new PlayerControls();
        }

        private void OnEnable() => controls.Flight.AimPoint.Enable();

        private void OnDisable() => controls.Flight.AimPoint.Disable();

        private void Update()
        {
            var profile = projection.Profile;
            var aim = controls.Flight.AimPoint.ReadValue<Vector2>();
            var reach = profile.targetPickRadius * Screen.height;
            var nearest = reach * reach;
            Ship player = null;
            Ship pointed = null;
            foreach (var ship in indicators.Keys)
            {
                if (ship.CompareTag(TagNames.Player))
                {
                    player = ship;
                    continue;
                }
                if (!ship.gameObject.activeInHierarchy || ship.Lock.IsConcealed) continue;
                var point = worldCamera.WorldToScreenPoint(ship.transform.position);
                var distance = ((Vector2)point - aim).sqrMagnitude;
                if (point.z < 0f || distance > nearest) continue;
                nearest = distance;
                pointed = ship;
            }
            var sensor = player ? player.Targeting : null;
            var locked = sensor ? sensor.CurrentTarget as Ship : null;
            var target = locked ? locked : pointed;
            foreach (var (ship, indicator) in indicators)
                indicator.Present(player, ship == target, profile);
        }

        public void SetOpacity(float opacity) => visibility.alpha = worldVisibility.alpha = opacity;

        private void Add(Ship ship)
        {
            indicators.Add(ship, ShipStatusIndicator.Create(canvas, worldCamera, ship, font, textMaterial,
                projection, () => visibility.alpha > 0f));
            LockOnIndicator reticle = null;
            var appearance = InstrumentAppear.Create(worldVisibility.transform, projection,
                () => worldVisibility.alpha > 0f && reticle.Visible);
            var pose = ship.VisualRig ? ship.VisualRig.transform : ship.transform;
            appearance.transform.localScale = pose.lossyScale;
            reticle = Instantiate(reticlePrefab, appearance.transform);
            reticle.Initialize(ship.Lock, pose);
            reticles.Add(ship, appearance);
        }

        private void Remove(Ship ship)
        {
            Destroy(indicators[ship].gameObject);
            indicators.Remove(ship);
            if (reticles.Remove(ship, out var reticle)) Destroy(reticle.gameObject);
        }

        private void OnDestroy()
        {
            controls.Dispose();
            if (worldVisibility) Destroy(worldVisibility.gameObject);
            if (ships == null) return;
            ships.OnAdd -= Add;
            ships.OnRemove -= Remove;
        }
    }
}
