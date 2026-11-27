using System;
using System.Collections.Generic;
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
        private const float AimTravelPixels = 60f;

        private CanvasGroup group;
        private Func<bool> used;
        private Func<bool> dismissed;
        private Func<bool> waiting;
        private float fadeAt = float.PositiveInfinity;

        // The keystone line sits lowest; weapon lines stack above it, the primary on top.
        public static IReadOnlyList<FirstRunPrompt> Show(Transform parent, Font keyFont, Font wordFont,
            Material textMaterial, IFlightInput input, string keystone, IWeaponReadouts weapons, Func<bool> dismissed,
            Func<bool> waiting)
        {
            Check(input, dismissed, waiting);
            var lines = new List<FirstRunPrompt>();
            var y = BottomY;
            lines.Add(Create(parent, keyFont, wordFont, textMaterial, "SPACE", keystone, y, () => input.BoostDown,
                dismissed, waiting));
            if (weapons == null) return lines;
            for (var i = weapons.Slots.Count - 1; i >= 0; i--)
            {
                var slot = weapons.Slots[i];
                y += LineStep;
                lines.Add(Create(parent, keyFont, wordFont, textMaterial, Key(slot), $"FIRE {weapons.DisplayName(slot)}",
                    y, Used(input, slot), dismissed, waiting));
            }
            return lines;
        }

        // Aim sits lowest, thrust on top, in the order a pilot reaches for them.
        public static IReadOnlyList<FirstRunPrompt> ShowMovement(Transform parent, Font keyFont, Font wordFont,
            Material textMaterial, IFlightInput input, Func<bool> dismissed, Func<bool> waiting)
        {
            Check(input, dismissed, waiting);
            return new[]
            {
                Create(parent, keyFont, wordFont, textMaterial, "W/S", "THRUST", BottomY + 2f * LineStep,
                    () => input.Thrust != 0f, dismissed, waiting),
                Create(parent, keyFont, wordFont, textMaterial, "A/D", "STRAFE", BottomY + LineStep,
                    () => input.Strafe != 0f, dismissed, waiting),
                Create(parent, keyFont, wordFont, textMaterial, "MOUSE", "AIM", BottomY, Aimed(input), dismissed,
                    waiting, "MOVE"),
            };
        }

        private static void Check(IFlightInput input, Func<bool> dismissed, Func<bool> waiting)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (dismissed == null) throw new ArgumentNullException(nameof(dismissed));
            if (waiting == null) throw new ArgumentNullException(nameof(waiting));
        }

        private static Func<bool> Aimed(IFlightInput input)
        {
            var from = input.AimScreenPoint;
            return () => (input.AimScreenPoint - from).sqrMagnitude >= AimTravelPixels * AimTravelPixels;
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

        private static FirstRunPrompt Create(Transform parent, Font keyFont, Font wordFont, Material textMaterial,
            string key, string action, float bottomY, Func<bool> used, Func<bool> dismissed, Func<bool> waiting,
            string verb = "PRESS")
        {
            var line = KeyPrompt.Create(parent, keyFont, wordFont, textMaterial, key, action.ToUpperInvariant(), verb);
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
            return prompt;
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
