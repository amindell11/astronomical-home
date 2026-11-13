using System;
using Game.Player;
using Ships.Command;
using UnityEngine;

namespace UI.Prompts
{
    public sealed class FirstRunPrompt : MonoBehaviour
    {
        private const float FadeSeconds = .3f;
        private const float BottomY = 124f;
        private const float LineStep = 24f;

        private CanvasGroup group;
        private Func<bool> used;
        private Func<bool> dismissed;
        private Func<bool> waiting;
        private float fadeAt = float.PositiveInfinity;

        // The keystone line sits lowest; weapon lines stack above it, the primary on top.
        public static void Show(Transform parent, Font keyFont, Font wordFont, Material textMaterial,
            IFlightInput input, string keystone, IWeaponReadouts weapons, Func<bool> dismissed, Func<bool> waiting)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (dismissed == null) throw new ArgumentNullException(nameof(dismissed));
            if (waiting == null) throw new ArgumentNullException(nameof(waiting));

            var y = BottomY;
            Create(parent, keyFont, wordFont, textMaterial, "SPACE", keystone, y, () => input.BoostDown, dismissed, waiting);
            if (weapons == null) return;
            for (var i = weapons.Slots.Count - 1; i >= 0; i--)
            {
                var slot = weapons.Slots[i];
                y += LineStep;
                Create(parent, keyFont, wordFont, textMaterial, Key(slot), $"FIRE {weapons.DisplayName(slot)}", y,
                    Used(input, slot), dismissed, waiting);
            }
        }

        private static string Key(WeaponSlot slot) => slot switch
        {
            WeaponSlot.Primary => "LMB",
            WeaponSlot.Secondary => "RMB",
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };

        private static Func<bool> Used(IFlightInput input, WeaponSlot slot) => slot switch
        {
            WeaponSlot.Primary => () => input.PrimaryFire,
            WeaponSlot.Secondary => () => input.SecondaryFire,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };

        private static void Create(Transform parent, Font keyFont, Font wordFont, Material textMaterial,
            string key, string action, float bottomY, Func<bool> used, Func<bool> dismissed, Func<bool> waiting)
        {
            var line = KeyPrompt.Create(parent, keyFont, wordFont, textMaterial, key, action.ToUpperInvariant());
            line.name = "FirstRunPrompt";
            var rect = (RectTransform)line.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottomY);

            var prompt = line.gameObject.AddComponent<FirstRunPrompt>();
            prompt.group = line.gameObject.AddComponent<CanvasGroup>();
            prompt.used = used;
            prompt.dismissed = dismissed;
            prompt.waiting = waiting;
            prompt.group.alpha = waiting() ? 0f : 1f;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (float.IsPositiveInfinity(fadeAt))
            {
                var done = used() || dismissed();
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
