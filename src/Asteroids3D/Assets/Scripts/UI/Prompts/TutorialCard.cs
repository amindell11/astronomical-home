using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Prompts
{
    public sealed class TutorialCard : MonoBehaviour
    {
        public enum Spot { UnderBanner, High }

        private const float FadeSeconds = .3f;
        private const float UnderBannerY = 176f;
        private const float HighY = 270f;
        private const float Width = 440f;
        private const int TextSize = 15;
        private const float SkipGap = 10f;

        private CanvasGroup group;
        private KeyPrompt skip;
        private Func<bool> ended;
        private Func<bool> skippable;
        private float shownAt;
        private float fadeAt = float.PositiveInfinity;

        public string Message { get; private set; }

        public static TutorialCard Show(Transform parent, Font keyFont, Font wordFont, Material textMaterial,
            string text, Spot spot, Func<bool> ended, Func<bool> skippable = null)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("A card needs its text.", nameof(text));
            if (ended == null) throw new ArgumentNullException(nameof(ended));

            var go = new GameObject("TutorialCard", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, spot == Spot.High ? HighY : UnderBannerY);
            rect.sizeDelta = new Vector2(Width, 0f);

            var card = go.AddComponent<TutorialCard>();
            card.group = go.AddComponent<CanvasGroup>();
            card.group.alpha = 0f;
            card.Message = text;
            card.ended = ended;
            card.skippable = skippable;
            card.shownAt = Time.unscaledTime;

            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            label.gameObject.layer = go.layer;
            label.transform.SetParent(go.transform, false);
            label.font = wordFont;
            label.material = textMaterial;
            label.fontSize = TextSize;
            label.color = Color.white;
            label.alignment = TextAnchor.LowerCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(.5f, 0f);
            labelRect.pivot = new Vector2(.5f, 0f);
            labelRect.sizeDelta = new Vector2(Width, label.preferredHeight);
            labelRect.anchoredPosition = Vector2.zero;

            if (skippable == null) return card;
            card.skip = KeyPrompt.Create(go.transform, keyFont, wordFont, textMaterial, "LMB", "CONTINUE");
            var skipRect = (RectTransform)card.skip.transform;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(.5f, 0f);
            skipRect.pivot = new Vector2(.5f, 1f);
            skipRect.anchoredPosition = new Vector2(0f, -SkipGap);
            card.skip.gameObject.SetActive(false);
            return card;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (float.IsPositiveInfinity(fadeAt) && ended()) fadeAt = now;
            if (skip) skip.gameObject.SetActive(float.IsPositiveInfinity(fadeAt) && skippable());
            group.alpha = float.IsPositiveInfinity(fadeAt)
                ? Mathf.Clamp01((now - shownAt) / FadeSeconds)
                : Mathf.Min(group.alpha, 1f - Mathf.Clamp01((now - fadeAt) / FadeSeconds));
            if (!float.IsPositiveInfinity(fadeAt) && group.alpha == 0f) Destroy(gameObject);
        }
    }
}
