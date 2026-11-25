using UI.Placement;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Popups
{
    /// <summary>
    /// "+N HULL" over a kill: pinned to where the ship died, starting above its fireball and
    /// rising while it fades, then destroyed. The dark edge keeps it legible over fire. Each kill shows its own.
    /// </summary>
    public sealed class HullGainPopup : MonoBehaviour
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

        public static HullGainPopup Show(Canvas canvas, Camera worldCamera, Font font, Material material,
            float amount, Vector3 worldPoint)
        {
            var go = new GameObject("Hull gain", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.material = material;
            text.fontSize = 30;
            text.color = StatusBarUI.HullTint;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = $"+{Mathf.Max(1, Mathf.RoundToInt(amount))} HULL";
            var edge = go.AddComponent<Outline>();
            edge.effectColor = Edge;
            edge.effectDistance = new Vector2(2f, -2f);
            var popup = go.AddComponent<HullGainPopup>();
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
