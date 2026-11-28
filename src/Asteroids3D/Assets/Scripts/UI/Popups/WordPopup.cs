using UI.Placement;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Popups
{
    /// <summary>
    /// A short word in its own colour over a world point, such as "+N HULL" where a kill died: it starts
    /// above the point, clear of any fireball, and rises while it fades, then is destroyed. The dark edge
    /// keeps it legible over fire. Each word shows its own.
    /// </summary>
    public sealed class WordPopup : MonoBehaviour
    {
        public const float Seconds = 1.5f;
        private const float FadeSeconds = .6f;
        public const float StartLiftPixels = 60f;
        private const float RisePixels = 90f;
        private static readonly Color Edge = new(0f, 0f, 0f, .85f);

        private Canvas canvas;
        private Camera worldCamera;
        private RectTransform rect;
        private Text text;
        private Vector3 origin;
        private float bornAt;

        public static WordPopup Show(Canvas canvas, Camera worldCamera, Font font, Material material,
            string word, Color color, Vector3 worldPoint)
        {
            var go = new GameObject(word, typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.material = material;
            text.fontSize = 30;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = word;
            var edge = go.AddComponent<Outline>();
            edge.effectColor = Edge;
            edge.effectDistance = new Vector2(2f, -2f);
            var popup = go.AddComponent<WordPopup>();
            popup.canvas = canvas;
            popup.worldCamera = worldCamera;
            popup.rect = (RectTransform)go.transform;
            popup.text = text;
            popup.origin = worldPoint;
            popup.bornAt = Time.time;
            popup.Place(0f);
            return popup;
        }

        private void LateUpdate()
        {
            var age = Time.time - bornAt;
            if (age >= Seconds)
            {
                Destroy(gameObject);
                return;
            }

            var ink = text.color;
            ink.a = Mathf.Clamp01((Seconds - age) / FadeSeconds);
            text.color = ink;
            Place(age / Seconds);
        }

        private void Place(float progress)
        {
            Vector2 point = worldCamera.WorldToScreenPoint(origin);
            var eased = 1f - (1f - progress) * (1f - progress);
            point.y += (StartLiftPixels + RisePixels * eased) * canvas.scaleFactor;
            ScreenPin.Place(rect, canvas, point);
        }
    }
}
