using System;
using Objectives;
using Substrate.Services.Objectives;
using UnityEngine;
using UnityEngine.UI;
using UI.Projection;

namespace UI.Objectives
{
    public sealed class ObjectiveMarkers : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseScale = .15f;
        [SerializeField] private Color baseColor = Color.yellow;
        [SerializeField] private Sprite exitIcon;
        [SerializeField] private Sprite lullIcon;
        [SerializeField] private Color exitColour = new Color32(0xBF, 0xF4, 0xFF, 0xFF);
        [SerializeField] private Color lullColour = new Color32(0xE0, 0xA6, 0xFF, 0xFF);

        private const float ScreenIconSize = 22f;
        private const float MapIconSize = 13.75f;

        private readonly Vector3[] corners = new Vector3[4];
        private IObjectiveService objectives;
        private RectTransform minimapRect;
        private Camera minimapCamera;
        private Camera worldCamera;
        private Canvas canvas;
        private Image screenBerth;
        private Image minimapBerth;
        private NavTab screenTab;
        private NavTab mapTab;

        private void Awake() => enabled = false;

        public void Initialize(IObjectiveService source, Camera world, Camera map, RectTransform minimap, Canvas hud,
            CanopyProjection projection = null, Func<bool> isVisible = null)
        {
            if (source == null || !world || !map || !minimap || !hud || !icon)
                throw new InvalidOperationException($"ObjectiveMarkers on '{name}' needs its objective source, cameras, minimap, canvas and icon.");
            if (!exitIcon || !lullIcon)
                throw new InvalidOperationException($"ObjectiveMarkers on '{name}' needs its exit and lull beacon sprites.");
            objectives = source;
            worldCamera = world;
            minimapCamera = map;
            minimapRect = minimap;
            canvas = hud;
            screenBerth = Beacon(hud.transform, "Extraction direction", ScreenIconSize, out screenTab);
            minimapBerth = Beacon(hud.transform, "Extraction minimap", MapIconSize, out mapTab);
            InstrumentAppear.Attach(screenBerth.gameObject, projection,
                () => screenBerth.enabled && (isVisible?.Invoke() ?? true));
            InstrumentAppear.Attach(minimapBerth.gameObject, projection,
                () => minimapBerth.enabled && (isVisible?.Invoke() ?? true));
            icon.enabled = false;
            enabled = true;
        }

        private static Image Beacon(Transform hud, string name, float size, out NavTab tab)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.gameObject.layer = hud.gameObject.layer;
            image.transform.SetParent(hud, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.rectTransform.sizeDelta = Vector2.one * size;
            image.raycastTarget = false;
            image.enabled = false;
            tab = NavTab.Create(image.transform, name + " tab");
            tab.enabled = false;
            return image;
        }

        private void LateUpdate()
        {
            var state = objectives.SpineState;
            var target = objectives.SpineTarget;
            var berth = (state == ObjectiveType.ExtractionChallenge || state == ObjectiveType.Survive) && target;
            var generic = (state == ObjectiveType.Explore || state == ObjectiveType.KeyAcquired) && target;
            var lull = state == ObjectiveType.Survive;
            var sprite = lull ? lullIcon : exitIcon;
            var colour = lull ? lullColour : exitColour;
            var reach = lull ? NavTab.LullReach : NavTab.ExitReach;
            screenBerth.sprite = minimapBerth.sprite = sprite;
            screenBerth.color = minimapBerth.color = colour;
            screenBerth.enabled = minimapBerth.enabled = berth;
            if (!berth)
                screenTab.enabled = mapTab.enabled = false;
            icon.enabled = generic;
            if (!berth && !generic) return;

            var mapPoint = minimapCamera.WorldToViewportPoint(target.position);
            var mapOffset = new Vector2(mapPoint.x - .5f, mapPoint.y - .5f);
            if (generic)
            {
                icon.rectTransform.anchoredPosition = Vector2.Scale(Vector2.ClampMagnitude(mapOffset, .5f), minimapRect.rect.size);
                var pulse = Mathf.Sin(Time.time * pulseSpeed);
                icon.color = Color.Lerp(baseColor, Color.white, pulse * .5f + .5f);
                icon.rectTransform.localScale = Vector3.one * (1f + pulseScale * pulse);
            }
            if (!berth) return;

            var mapBounds = MapBounds(minimapRect, canvas, corners);
            var radius = Mathf.Min(mapBounds.width, mapBounds.height) * .5f;
            var offset = mapOffset * radius * 2f;
            var inset = MapIconSize * .5f + 1f;
            if (offset.magnitude > radius - inset)
            {
                var dir = offset.normalized;
                minimapBerth.rectTransform.anchoredPosition =
                    mapBounds.center + dir * (radius - 1f - NavTab.Extent(MapIconSize, reach));
                mapTab.Aim(Vector2.zero, dir, MapIconSize, reach, colour);
            }
            else
            {
                minimapBerth.rectTransform.anchoredPosition = mapBounds.center + offset;
                mapTab.enabled = false;
            }

            var projected = worldCamera.WorldToScreenPoint(target.position);
            screenBerth.enabled = projected.z > 0f;
            var viewport = worldCamera.pixelRect;
            var offScreen = PinToScreenEdge(projected, viewport, canvas, mapBounds, out var canvasPosition);
            screenBerth.rectTransform.anchoredPosition = canvasPosition;
            if (screenBerth.enabled && offScreen)
                screenTab.Aim(Vector2.zero, ((Vector2)projected - viewport.center).normalized,
                    ScreenIconSize, reach, colour);
            else
                screenTab.enabled = false;
        }

        internal static Rect MapBounds(RectTransform minimap, Canvas canvas, Vector3[] corners)
        {
            minimap.GetWorldCorners(corners);
            var canvasRect = (RectTransform)canvas.transform;
            var lower = (Vector2)canvasRect.InverseTransformPoint(corners[0]) - canvasRect.rect.min;
            var upper = (Vector2)canvasRect.InverseTransformPoint(corners[2]) - canvasRect.rect.min;
            return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
        }

        internal static bool PinToScreenEdge(Vector2 projected, Rect viewport, Canvas canvas, Rect mapBounds,
            out Vector2 canvasPosition)
        {
            var padding = 40f * canvas.scaleFactor;
            var safe = Rect.MinMaxRect(viewport.xMin + padding, viewport.yMin + padding,
                viewport.xMax - padding, viewport.yMax - 70f * canvas.scaleFactor);
            var location = ClampToEdge(projected, viewport.center, safe);
            var offScreen = !safe.Contains(projected);
            if (offScreen && mapBounds.Contains(location / canvas.scaleFactor))
                location.y = mapBounds.yMin * canvas.scaleFactor - padding;
            canvasPosition = location / canvas.scaleFactor;
            return offScreen;
        }

        internal static Vector2 ClampToEdge(Vector2 point, Vector2 center, Rect bounds)
        {
            var direction = point - center;
            var x = direction.x == 0f ? float.PositiveInfinity :
                ((direction.x > 0f ? bounds.xMax : bounds.xMin) - center.x) / direction.x;
            var y = direction.y == 0f ? float.PositiveInfinity :
                ((direction.y > 0f ? bounds.yMax : bounds.yMin) - center.y) / direction.y;
            return center + direction * Mathf.Min(1f, Mathf.Min(x, y));
        }
    }
}
