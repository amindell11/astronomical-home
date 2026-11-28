using System;
using UI.Placement;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Prompts
{
    public sealed class WorldLabel : MonoBehaviour
    {
        private const float FadeSeconds = .3f;
        private const int TextSize = 13;
        private const float LiftPixels = 26f;
        private static readonly Color Edge = new(0f, 0f, 0f, .85f);

        private Canvas canvas;
        private Camera worldCamera;
        private RectTransform rect;
        private CanvasGroup group;
        private Vector3 world;
        private Func<bool> ended;
        private float shownAt;
        private float fadeAt = float.PositiveInfinity;

        public string Message { get; private set; }

        public static WorldLabel Show(Canvas canvas, Camera worldCamera, Font font, Material material, string text,
            Color tint, Vector3 worldPoint, Func<bool> ended)
        {
            if (!canvas) throw new ArgumentNullException(nameof(canvas));
            if (!worldCamera) throw new ArgumentNullException(nameof(worldCamera));
            if (ended == null) throw new ArgumentNullException(nameof(ended));

            var go = new GameObject("WorldLabel", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.material = material;
            label.fontSize = TextSize;
            label.color = tint;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            var edge = go.AddComponent<Outline>();
            edge.effectColor = Edge;
            edge.effectDistance = new Vector2(1f, -1f);

            var pin = go.AddComponent<WorldLabel>();
            pin.canvas = canvas;
            pin.worldCamera = worldCamera;
            pin.rect = (RectTransform)go.transform;
            pin.group = go.AddComponent<CanvasGroup>();
            pin.group.alpha = 0f;
            pin.world = worldPoint;
            pin.ended = ended;
            pin.Message = text;
            pin.shownAt = Time.unscaledTime;
            pin.Place();
            return pin;
        }

        private void LateUpdate()
        {
            var now = Time.unscaledTime;
            if (float.IsPositiveInfinity(fadeAt) && ended()) fadeAt = now;
            group.alpha = float.IsPositiveInfinity(fadeAt)
                ? Mathf.Clamp01((now - shownAt) / FadeSeconds)
                : Mathf.Min(group.alpha, 1f - Mathf.Clamp01((now - fadeAt) / FadeSeconds));
            if (!float.IsPositiveInfinity(fadeAt) && group.alpha == 0f)
            {
                Destroy(gameObject);
                return;
            }
            Place();
        }

        private void Place()
        {
            Vector2 point = worldCamera.WorldToScreenPoint(world);
            point.y += LiftPixels * canvas.scaleFactor;
            ScreenPin.Place(rect, canvas, point);
        }
    }
}
