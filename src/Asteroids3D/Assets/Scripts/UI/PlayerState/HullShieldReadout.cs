using System;
using Ships.Damage;
using UI.Projection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    /// <summary>
    /// The player's shield and hull bars under the Keystone weapon row, after the #1249 canopy mock.
    /// A <see cref="StatusBarUI"/> binds one resource for life, so rebinding replaces both bars.
    /// </summary>
    public sealed class HullShieldReadout : MonoBehaviour
    {
        // The Keystone row is bottom-centre at y 58.5; its circle ends near y 32.
        private static readonly Vector2 BarSize = new(88f, 6f);
        private const float BarX = 56f;
        private const float BarY = 22f;
        private const float LabelY = 13f;
        private static readonly Color LabelInk = new(.953f, .949f, .875f, .6f);

        private StatusBarUI shield;
        private StatusBarUI hull;

        public static HullShieldReadout Create(Transform parent, Font font, Material material,
            CanopyProjection projection, Func<bool> isVisible)
        {
            if (isVisible == null) throw new ArgumentNullException(nameof(isVisible));

            var go = new GameObject("HullShieldReadout", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            Label("SHIELD", -BarX);
            Label("HULL", BarX);
            InstrumentAppear.Attach(go, projection, isVisible);
            return go.AddComponent<HullShieldReadout>();

            void Label(string caption, float x)
            {
                var text = new GameObject(caption, typeof(RectTransform)).AddComponent<Text>();
                text.gameObject.layer = go.layer;
                text.transform.SetParent(go.transform, false);
                text.font = font;
                text.material = material;
                text.fontSize = 5;
                text.color = LabelInk;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(BarSize.x, 8f);
                text.rectTransform.anchoredPosition = new Vector2(x, LabelY);
                text.text = caption;
            }
        }

        public void Initialize(IDamageEvents damage)
        {
            if (damage == null) throw new ArgumentNullException(nameof(damage));
            // Destroy is deferred, so a stale bar would match a same-frame lookup.
            if (shield)
            {
                shield.gameObject.SetActive(false);
                Destroy(shield.gameObject);
            }

            if (hull)
            {
                hull.gameObject.SetActive(false);
                Destroy(hull.gameObject);
            }

            shield = StatusBarUI.Create(transform, damage.Shield, StatusBarUI.ShieldTint, new Vector2(-BarX, BarY), BarSize);
            hull = StatusBarUI.Create(transform, damage.Health, StatusBarUI.HullTint, new Vector2(BarX, BarY), BarSize,
                pulsesWhenLow: true);
        }
    }
}
