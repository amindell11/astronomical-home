using System;
using Substrate.Sectors.Elements.Elites;
using UI.Projection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Elites
{
    public sealed class EliteReadout : MonoBehaviour
    {
        private const float BarWidth = 240f;
        private const float HullHeight = 3f;
        private const float ShieldHeight = 1.5f;
        // The extraction announcement's timings, so the two banners read as one style.
        private const float BannerSeconds = 2.05f;
        private const float BannerFadeIn = .22f;
        private const float BannerFadeOut = .45f;

        private RectTransform bar;
        private Text nameLabel;
        private Image hullFill;
        private Image shieldFill;
        private Text banner;
        private IEliteStatus elite;
        private bool wasPresent;
        private float arrivedAt = float.NegativeInfinity;

        public static EliteReadout Create(Transform parent, Font labelFont, Font headingFont, Material textMaterial,
            CanopyProjection projection, Func<bool> isVisible)
        {
            if (isVisible == null) throw new ArgumentNullException(nameof(isVisible));

            var go = new GameObject("EliteReadout", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            var barObject = new GameObject("Elite bar", typeof(RectTransform));
            barObject.layer = go.layer;
            barObject.transform.SetParent(go.transform, false);
            var bar = (RectTransform)barObject.transform;
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -44f);
            bar.sizeDelta = new Vector2(BarWidth, 24f);

            var nameLabel = CreateText(bar, "Elite name", labelFont, textMaterial, 10, new Vector2(BarWidth, 16f));
            nameLabel.rectTransform.anchorMin = nameLabel.rectTransform.anchorMax = nameLabel.rectTransform.pivot =
                new Vector2(.5f, 1f);
            nameLabel.alignment = TextAnchor.UpperCenter;
            var shieldFill = CreateBar(bar, "Elite shield", -16f, ShieldHeight, Color.white);
            CreateBar(bar, "Elite hull track", -19.5f, HullHeight, new Color(1f, 1f, 1f, .25f));
            var hullFill = CreateBar(bar, "Elite hull", -19.5f, HullHeight, Color.white);
            InstrumentAppear.Attach(barObject, projection, isVisible);

            var banner = CreateText(go.transform, "Elite arrival", headingFont, textMaterial, 13, new Vector2(420f, 28f));
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = banner.rectTransform.pivot =
                new Vector2(.5f, .5f);
            banner.alignment = TextAnchor.MiddleCenter;
            banner.enabled = false;
            InstrumentAppear.Attach(banner.gameObject, projection,
                () => banner.enabled && banner.color.a > 0f && isVisible());

            barObject.SetActive(false);
            var readout = go.AddComponent<EliteReadout>();
            readout.bar = bar;
            readout.nameLabel = nameLabel;
            readout.hullFill = hullFill;
            readout.shieldFill = shieldFill;
            readout.banner = banner;
            return readout;
        }

        private static Text CreateText(Transform parent, string name, Font font, Material material, int size, Vector2 bounds)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.gameObject.layer = parent.gameObject.layer;
            text.transform.SetParent(parent, false);
            text.rectTransform.sizeDelta = bounds;
            text.font = font;
            text.material = material;
            text.fontSize = size;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        // Left-anchored, so a fill's width reads its fraction from the bar's left end.
        private static Image CreateBar(Transform parent, string name, float y, float height, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0f, 1f);
            image.rectTransform.anchoredPosition = new Vector2(0f, y);
            image.rectTransform.sizeDelta = new Vector2(BarWidth, height);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Null unbinds and hides the readout.</summary>
        public void Bind(IEliteStatus elite)
        {
            this.elite = elite;
            wasPresent = false;
            arrivedAt = float.NegativeInfinity;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            var present = elite != null && elite.Present;
            if (present && !wasPresent)
            {
                arrivedAt = Time.unscaledTime;
                nameLabel.text = banner.text = elite.Name.ToUpperInvariant();
            }
            wasPresent = present;

            bar.gameObject.SetActive(present);
            if (present)
            {
                hullFill.rectTransform.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(elite.HullPct), HullHeight);
                shieldFill.rectTransform.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(elite.ShieldPct), ShieldHeight);
            }

            var elapsed = Time.unscaledTime - arrivedAt;
            banner.enabled = present && elapsed < BannerSeconds;
            banner.color = new Color(1f, 1f, 1f,
                Mathf.Clamp01(Mathf.Min(elapsed / BannerFadeIn, (BannerSeconds - elapsed) / BannerFadeOut)));
        }
    }
}
