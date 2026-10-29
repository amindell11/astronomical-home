using System;
using Objectives;
using Substrate.Services.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Objectives
{
    public sealed class ObjectiveMarkers : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseScale = .15f;
        [SerializeField] private Color baseColor = Color.yellow;

        private readonly Vector3[] corners = new Vector3[4];
        private IObjectiveService objectives;
        private RectTransform minimapRect;
        private Camera minimapCamera;
        private Camera worldCamera;
        private Canvas canvas;
        private BerthGraphic screenBerth;
        private BerthGraphic minimapBerth;

        private void Awake() => enabled = false;

        public void Initialize(IObjectiveService source, Camera world, Camera map, RectTransform minimap, Canvas hud)
        {
            if (source == null || !world || !map || !minimap || !hud || !icon)
                throw new InvalidOperationException($"ObjectiveMarkers on '{name}' needs its objective source, cameras, minimap, canvas and icon.");
            objectives = source;
            worldCamera = world;
            minimapCamera = map;
            minimapRect = minimap;
            canvas = hud;
            screenBerth = BerthGraphic.Create(hud.transform, false);
            minimapBerth = BerthGraphic.Create(hud.transform, true);
            icon.enabled = false;
            enabled = true;
        }

        private void LateUpdate()
        {
            var state = objectives.SpineState;
            var target = objectives.SpineTarget;
            var extraction = state == ObjectiveType.ExtractionChallenge && target;
            var generic = (state == ObjectiveType.Explore || state == ObjectiveType.KeyAcquired) && target;
            screenBerth.enabled = minimapBerth.enabled = extraction;
            icon.enabled = generic;
            if (!extraction && !generic) return;

            var mapPoint = minimapCamera.WorldToViewportPoint(target.position);
            var mapOffset = new Vector2(mapPoint.x - .5f, mapPoint.y - .5f);
            if (generic)
            {
                icon.rectTransform.anchoredPosition = Vector2.Scale(Vector2.ClampMagnitude(mapOffset, .5f), minimapRect.rect.size);
                var pulse = Mathf.Sin(Time.time * pulseSpeed);
                icon.color = Color.Lerp(baseColor, Color.white, pulse * .5f + .5f);
                icon.rectTransform.localScale = Vector3.one * (1f + pulseScale * pulse);
            }
            if (!extraction) return;

            minimapRect.GetWorldCorners(corners);
            var canvasRect = (RectTransform)canvas.transform;
            var lower = (Vector2)canvasRect.InverseTransformPoint(corners[0]) - canvasRect.rect.min;
            var upper = (Vector2)canvasRect.InverseTransformPoint(corners[2]) - canvasRect.rect.min;
            var mapBounds = Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
            var radius = Mathf.Min(mapBounds.width, mapBounds.height) * .5f;
            var inset = minimapBerth.rectTransform.rect.width * .5f + 1f;
            minimapBerth.rectTransform.anchoredPosition = (mapBounds.center +
                Vector2.ClampMagnitude(mapOffset * radius * 2f, radius - inset));

            var projected = worldCamera.WorldToScreenPoint(target.position);
            screenBerth.enabled = projected.z > 0f;
            var viewport = worldCamera.pixelRect;
            var padding = 40f * canvas.scaleFactor;
            var safe = Rect.MinMaxRect(viewport.xMin + padding, viewport.yMin + padding,
                viewport.xMax - padding, viewport.yMax - 70f * canvas.scaleFactor);
            var location = ClampToEdge(projected, viewport.center, safe);
            if (!safe.Contains(projected) && mapBounds.Contains(location / canvas.scaleFactor))
                location.y = mapBounds.yMin * canvas.scaleFactor - padding;
            screenBerth.rectTransform.anchoredPosition = location / canvas.scaleFactor;
            var direction = (Vector2)projected - viewport.center;
            screenBerth.rectTransform.localEulerAngles = new Vector3(0, 0,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
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
