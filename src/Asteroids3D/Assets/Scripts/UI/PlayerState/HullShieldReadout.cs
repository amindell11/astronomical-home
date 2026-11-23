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
    /// Kill refills flash above the hull bar, summing while the flash is still up.
    /// </summary>
    public sealed class HullShieldReadout : MonoBehaviour
    {
        // The Keystone row is bottom-centre at y 58.5; its circle ends near y 32.
        private static readonly Vector2 BarSize = new(88f, 6f);
        private const float BarX = 56f;
        private const float BarY = 22f;
        private const float LabelY = 13f;
        private static readonly Color LabelInk = new(.953f, .949f, .875f, .6f);
        private const float GainY = 31f;
        private const float GainHoldSeconds = 1.6f;
        private const float GainFadeSeconds = .5f;

        private StatusBarUI shield;
        private StatusBarUI hull;
        private Text gain;
        private float gainShown;
        private float gainUntil;

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
            Label("SHIELD", -BarX, LabelY, 5, LabelInk);
            Label("HULL", BarX, LabelY, 5, LabelInk);
            var gain = Label("HULL GAIN", BarX, GainY, 8, StatusBarUI.HullTint);
            gain.gameObject.SetActive(false);
            InstrumentAppear.Attach(go, projection, isVisible);
            var readout = go.AddComponent<HullShieldReadout>();
            readout.gain = gain;
            return readout;

            Text Label(string caption, float x, float y, int size, Color ink)
            {
                var text = new GameObject(caption, typeof(RectTransform)).AddComponent<Text>();
                text.gameObject.layer = go.layer;
                text.transform.SetParent(go.transform, false);
                text.font = font;
                text.material = material;
                text.fontSize = size;
                text.color = ink;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(BarSize.x, 8f);
                text.rectTransform.anchoredPosition = new Vector2(x, y);
                text.text = caption;
                return text;
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

        public void ShowGain(float amount)
        {
            if (amount <= 0f) return;
            gainShown = gain.gameObject.activeSelf ? gainShown + amount : amount;
            gainUntil = Time.time + GainHoldSeconds;
            gain.text = $"+{Mathf.Max(1, Mathf.RoundToInt(gainShown))} HULL";
            gain.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!gain.gameObject.activeSelf) return;
            var left = gainUntil - Time.time;
            if (left <= 0f)
            {
                gain.gameObject.SetActive(false);
                return;
            }

            var ink = gain.color;
            ink.a = Mathf.Clamp01(left / GainFadeSeconds);
            gain.color = ink;
        }
    }
}
