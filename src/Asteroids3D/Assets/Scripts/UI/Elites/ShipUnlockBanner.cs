using System;
using UI.Projection;
using UI.Screens.Windows;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Elites
{
    /// <summary>
    /// The achievement card shown when an elite kill earns a ship: a green SHIP UNLOCKED tag between
    /// two rules that draw outward, the ship's name, and where to find it. It sits above centre so the
    /// companion's comm line below can comment on it.
    /// </summary>
    public sealed class ShipUnlockBanner : MonoBehaviour
    {
        private const float HoldSeconds = 4.2f;
        private const float FadeIn = .22f;
        private const float FadeOut = .6f;
        private const float RuleDrawSeconds = .4f;
        private const float RuleWidth = 64f;
        private const float RuleGap = 52f;
        private const float Lift = 64f;

        private CanvasGroup group;
        private Text shipName;
        private RectTransform leftRule;
        private RectTransform rightRule;
        private float shownAt = float.NegativeInfinity;

        public static ShipUnlockBanner Create(Transform parent, Font labelFont, Font headingFont, Material textMaterial,
            CanopyProjection projection, Func<bool> isVisible)
        {
            if (isVisible == null) throw new ArgumentNullException(nameof(isVisible));

            var go = new GameObject("ShipUnlockBanner", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, Lift);
            rect.sizeDelta = new Vector2(420f, 60f);

            // The fade gets its own group: InstrumentAppear owns the root's alpha.
            var card = new GameObject("Card", typeof(RectTransform));
            card.layer = go.layer;
            card.transform.SetParent(go.transform, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = Vector2.zero;
            cardRect.anchorMax = Vector2.one;
            cardRect.sizeDelta = Vector2.zero;

            var banner = go.AddComponent<ShipUnlockBanner>();
            banner.group = card.AddComponent<CanvasGroup>();
            banner.group.alpha = 0f;
            banner.group.interactable = banner.group.blocksRaycasts = false;

            var tag = CreateText(card.transform, "Tag", labelFont, textMaterial, 9, 14f, WindowKit.Good);
            tag.text = "SHIP UNLOCKED";
            banner.leftRule = CreateRule(card.transform, "Left rule", -RuleGap, new Vector2(1f, .5f));
            banner.rightRule = CreateRule(card.transform, "Right rule", RuleGap, new Vector2(0f, .5f));
            banner.shipName = CreateText(card.transform, "Ship name", headingFont, textMaterial, 18, -4f, WindowKit.Ink);
            var where = CreateText(card.transform, "Where", labelFont, textMaterial, 7, -22f, WindowKit.Dim);
            where.text = "NOW IN YOUR HANGAR";

            InstrumentAppear.Attach(go, projection, () => banner.group.alpha > 0f && isVisible());
            return banner;
        }

        private static Text CreateText(Transform parent, string name, Font font, Material material, int size, float y,
            Color color)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.gameObject.layer = parent.gameObject.layer;
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(.5f, .5f);
            text.rectTransform.anchoredPosition = new Vector2(0f, y);
            text.rectTransform.sizeDelta = new Vector2(420f, size + 8f);
            text.font = font;
            text.material = material;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // Pivoted on its inner end, so growing its width draws it outward from the tag.
        private static RectTransform CreateRule(Transform parent, string name, float x, Vector2 pivot)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.pivot = pivot;
            image.rectTransform.anchoredPosition = new Vector2(x, 14f);
            image.rectTransform.sizeDelta = new Vector2(0f, .75f);
            image.color = WindowKit.Good;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        public void Show(string ship)
        {
            if (string.IsNullOrWhiteSpace(ship)) throw new ArgumentException("An unlocked ship needs a name.", nameof(ship));
            shipName.text = ship.ToUpperInvariant();
            shownAt = Time.unscaledTime;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            var elapsed = Time.unscaledTime - shownAt;
            group.alpha = elapsed < HoldSeconds
                ? Mathf.Clamp01(Mathf.Min(elapsed / FadeIn, (HoldSeconds - elapsed) / FadeOut))
                : 0f;
            var drawn = RuleWidth * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / RuleDrawSeconds));
            leftRule.sizeDelta = rightRule.sizeDelta = new Vector2(drawn, .75f);
        }
    }
}
