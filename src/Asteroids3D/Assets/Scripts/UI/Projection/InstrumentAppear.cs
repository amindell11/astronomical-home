using System;
using UnityEngine;

namespace UI.Projection
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class InstrumentAppear : MonoBehaviour
    {
        private CanvasGroup multiplier;
        private CanopyProjection projection;
        private Func<bool> isVisible;
        private readonly AppearanceEnvelope envelope = new();

        public static InstrumentAppear Create(Transform parent, CanopyProjection projection, Func<bool> isVisible)
        {
            var go = new GameObject("Instrument appearance", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            return Attach(go, projection, isVisible);
        }

        public static InstrumentAppear Attach(GameObject instrument, CanopyProjection projection, Func<bool> isVisible)
        {
            var appearance = instrument.AddComponent<InstrumentAppear>();
            appearance.projection = projection;
            appearance.isVisible = isVisible;
            return appearance;
        }

        private void Awake()
        {
            multiplier = GetComponent<CanvasGroup>();
            multiplier.interactable = multiplier.blocksRaycasts = false;
        }

        private void LateUpdate()
        {
            var effects = projection && projection.EffectsEnabled;
            multiplier.alpha = envelope.Sample(Time.unscaledTime, isVisible() && effects,
                effects ? projection.Settings.flicker : 0f);
        }

        private void OnDisable() => multiplier.alpha = envelope.Sample(Time.unscaledTime, false, 0f);
    }

    internal sealed class AppearanceEnvelope
    {
        private bool visible;
        private float startedAt = float.NegativeInfinity;
        private float lastStart = float.NegativeInfinity;

        public float Sample(float now, bool nextVisible, float strength)
        {
            if (nextVisible && !visible && now - lastStart >= 1f)
                startedAt = lastStart = now;
            if (!nextVisible) startedAt = float.NegativeInfinity;
            visible = nextVisible;
            var age = now - startedAt;
            var dip = age >= .09f && age < .19f || age >= .39f && age < .49f;
            return dip ? 1f - .17f * Mathf.Clamp01(strength) : 1f;
        }
    }
}
