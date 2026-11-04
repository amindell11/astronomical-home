using System;
using Game.Player;
using UnityEngine;

namespace UI.Prompts
{
    public sealed class KeystonePrompt : MonoBehaviour
    {
        private const string Key = "SPACE";
        private const float FadeSeconds = .3f;
        private const float BottomY = 124f;

        private CanvasGroup group;
        private IFlightInput input;
        private Func<bool> dismissed;
        private Func<bool> waiting;
        private float fadeAt = float.PositiveInfinity;

        public static KeystonePrompt Create(Transform parent, Font keyFont, Font wordFont, Material textMaterial,
            IFlightInput input, string keystone, Func<bool> dismissed, Func<bool> waiting)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (dismissed == null) throw new ArgumentNullException(nameof(dismissed));
            if (waiting == null) throw new ArgumentNullException(nameof(waiting));

            var line = KeyPrompt.Create(parent, keyFont, wordFont, textMaterial, Key, keystone.ToUpperInvariant());
            line.name = "KeystonePrompt";
            var rect = (RectTransform)line.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, BottomY);

            var prompt = line.gameObject.AddComponent<KeystonePrompt>();
            prompt.group = line.gameObject.AddComponent<CanvasGroup>();
            prompt.input = input;
            prompt.dismissed = dismissed;
            prompt.waiting = waiting;
            prompt.group.alpha = waiting() ? 0f : 1f;
            return prompt;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (float.IsPositiveInfinity(fadeAt))
            {
                var done = input.BoostDown || dismissed();
                if (waiting())
                {
                    group.alpha = 0f;
                    if (done) Destroy(gameObject);
                    return;
                }
                if (done) fadeAt = now;
            }
            group.alpha = 1f - Mathf.Clamp01((now - fadeAt) / FadeSeconds);
            if (group.alpha == 0f) Destroy(gameObject);
        }
    }
}
