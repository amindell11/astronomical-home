using System;
using System.Collections.Generic;
using Loot;
using Ships.Mods;
using UI.Projection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    /// <summary>
    /// The run's held lull mods as a row of badges right of the weapon instrument, one per mod
    /// with ×N for stacked copies. A badge shows the mod's icon, or its initials until the mod has
    /// art. It observes <see cref="IHeldMods.Changed"/>.
    /// </summary>
    public sealed class HeldModsReadout : MonoBehaviour
    {
        private const float BadgeSize = 22f;
        private const float CountWidth = 20f;
        private const float Gap = 5f;
        private const float RowWidth = 250f;
        private static readonly Color Ink = new(.953f, .949f, .875f);
        private static readonly Color ShipInk = new(.56f, .83f, .67f);
        private static readonly Color Backing = new(.043f, .078f, .149f, .65f);

        private readonly List<GameObject> badges = new();
        private Font font;
        private Material material;
        private IHeldMods mods;

        public static HeldModsReadout Create(Transform parent, Font font, Material material,
            CanopyProjection projection, Func<bool> isVisible)
        {
            if (isVisible == null) throw new ArgumentNullException(nameof(isVisible));

            var go = new GameObject("HeldModsReadout", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(0f, .5f);
            // The weapon keystone is bottom-centre at y 58.5, reaching about x ±125.
            rect.anchoredPosition = new Vector2(140f, 58.5f);
            rect.sizeDelta = new Vector2(RowWidth, BadgeSize);
            var readout = go.AddComponent<HeldModsReadout>();
            readout.font = font;
            readout.material = material;
            InstrumentAppear.Attach(go, projection, isVisible);
            return readout;
        }

        public void Initialize(IHeldMods source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (mods != null) mods.Changed -= Refresh;
            mods = source;
            mods.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (mods != null) mods.Changed -= Refresh;
        }

        private void Refresh()
        {
            foreach (var badge in badges)
            {
                // Destroy is deferred, so a stale badge would still match a same-frame lookup.
                badge.SetActive(false);
                Destroy(badge);
            }
            badges.Clear();

            var x = 0f;
            var y = 0f;
            foreach (var held in mods.All())
            {
                var slot = BadgeSize + (held.Count > 1 ? CountWidth : 0f) + Gap;
                if (x > 0f && x + slot > RowWidth)
                {
                    y += BadgeSize + Gap;
                    x = 0f;
                }

                badges.Add(Badge(held, new Vector2(x, y)));
                x += slot;
            }
        }

        private GameObject Badge(HeldMod held, Vector2 at)
        {
            var rect = Nest(held.Mod.DisplayName, transform);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = new Vector2(BadgeSize, BadgeSize);

            var ink = held.Mod as ShipMod ? ShipInk : Ink;
            Fill(Paint(rect, "Backing", Backing).rectTransform);
            Edge(rect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), ink);
            Edge(rect, "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1f), ink);
            Edge(rect, "Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 0f), ink);
            Edge(rect, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0f), ink);
            if (held.Mod.Icon)
            {
                var icon = Paint(rect, "Icon", ink);
                icon.sprite = held.Mod.Icon;
                icon.preserveAspect = true;
                Fill(icon.rectTransform, 3f);
            }
            else
                Fill(Lettering(rect, "Initials", Initials(held.Mod.DisplayName), ink, TextAnchor.MiddleCenter).rectTransform);

            if (held.Count <= 1) return rect.gameObject;
            var count = Lettering(rect, "Count", $"×{held.Count}", Ink, TextAnchor.MiddleLeft).rectTransform;
            count.anchorMin = count.anchorMax = count.pivot = new Vector2(0f, .5f);
            count.anchoredPosition = new Vector2(BadgeSize, 0f);
            count.sizeDelta = new Vector2(CountWidth, BadgeSize);
            return rect.gameObject;
        }

        private RectTransform Nest(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Image Paint(Transform parent, string name, Color color)
        {
            var image = Nest(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Lettering(Transform parent, string name, string value, Color color, TextAnchor align)
        {
            var text = Nest(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.material = material;
            text.fontSize = 9;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void Fill(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        private void Edge(RectTransform box, string name, Vector2 min, Vector2 max, Vector2 size, Color ink)
        {
            var rect = Paint(box, name, ink).rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = (min + max) * .5f;
            rect.sizeDelta = size;
        }

        internal static string Initials(string name)
        {
            var words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var initials = words.Length > 1
                ? $"{words[0][0]}{words[1][0]}"
                : words[0].Substring(0, Math.Min(2, words[0].Length));
            return initials.ToUpperInvariant();
        }
    }
}
