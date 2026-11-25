using UnityEngine;
using UnityEngine.UI;

namespace UI.Comms
{
    public sealed class CommLine : MonoBehaviour
    {
        private const int FontSize = 12;
        private const float Width = 560f;
        private const float Height = 18f;
        private const float BottomY = 92f;

        private CanvasGroup group;
        private Text text;
        private float shownAt = float.NegativeInfinity;
        private float holdSeconds;
        private float fadeSeconds;

        public bool Speaking => group.alpha > 0f;
        public string Line => text.text;

        public static CommLine Create(Transform parent, Font font, Material textMaterial)
        {
            var go = new GameObject("Comm line", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            // Own canvas and group: the recap hides and fades the HUD right after a death.
            go.AddComponent<Canvas>();
            var line = go.AddComponent<CommLine>();
            line.group = go.AddComponent<CanvasGroup>();
            line.group.ignoreParentGroups = true;
            line.group.alpha = 0f;

            line.text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            line.text.gameObject.layer = go.layer;
            line.text.transform.SetParent(go.transform, false);
            line.text.font = font;
            line.text.material = textMaterial;
            line.text.fontSize = FontSize;
            line.text.color = Color.white;
            line.text.alignment = TextAnchor.UpperCenter;
            line.text.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.text.verticalOverflow = VerticalWrapMode.Overflow;
            line.text.raycastTarget = false;
            line.text.text = string.Empty;
            var textRect = line.text.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(.5f, 0f);
            textRect.pivot = new Vector2(.5f, 0f);
            textRect.anchoredPosition = new Vector2(0f, BottomY);
            textRect.sizeDelta = new Vector2(Width, Height);
            return line;
        }

        public void Show(string line, float holdSeconds, float fadeSeconds)
        {
            text.text = line;
            this.holdSeconds = holdSeconds;
            this.fadeSeconds = fadeSeconds;
            shownAt = Time.unscaledTime;
            group.alpha = 1f;
        }

        public void Clear()
        {
            shownAt = float.NegativeInfinity;
            text.text = string.Empty;
            group.alpha = 0f;
        }

        private void Update() => group.alpha =
            1f - Mathf.Clamp01((Time.unscaledTime - shownAt - holdSeconds) / Mathf.Max(fadeSeconds, Mathf.Epsilon));
    }
}
