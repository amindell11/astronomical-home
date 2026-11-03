using System;
using Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Prompts
{
    public sealed class ControlPrompts : MonoBehaviour
    {
        private enum Prompt { Thrust, Strafe, Aim, Fire, Boost }

        private static readonly (string Key, string Action)[] Labels =
            { ("W / S", "THRUST"), ("A / D", "STRAFE"), ("MOUSE", "AIM"), ("CLICK", "FIRE"), ("SPACE", "BOOST") };

        private const float FadeSeconds = .3f;
        private const float AimSlackPixels = 10f;
        private const float BottomY = 124f;
        private const float RowWidth = 200f;
        private const float RowHeight = 16f;
        private const float Gutter = 6f;
        private const int KeySize = 10;
        private const int ActionSize = 12;

        private CanvasGroup[] rows;
        private float[] fadeAt;
        private IFlightInput input;
        private Func<bool> dismissed;
        private Vector2 aimOrigin;

        public static ControlPrompts Create(Transform parent, Font keyFont, Font actionFont, Material textMaterial,
            IFlightInput input, Func<bool> dismissed)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (dismissed == null) throw new ArgumentNullException(nameof(dismissed));

            var go = new GameObject("ControlPrompts", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, BottomY);
            rect.sizeDelta = new Vector2(RowWidth, RowHeight * Labels.Length);

            var prompts = go.AddComponent<ControlPrompts>();
            prompts.rows = new CanvasGroup[Labels.Length];
            prompts.fadeAt = new float[Labels.Length];
            for (var i = 0; i < Labels.Length; i++)
            {
                var row = new GameObject(((Prompt)i).ToString(), typeof(RectTransform)).AddComponent<CanvasGroup>();
                row.gameObject.layer = go.layer;
                row.transform.SetParent(rect, false);
                var rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = rowRect.anchorMax = new Vector2(.5f, 1f);
                rowRect.pivot = new Vector2(.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -i * RowHeight);
                rowRect.sizeDelta = new Vector2(RowWidth, RowHeight);
                CreateText(rowRect, "Key", keyFont, textMaterial, KeySize, Labels[i].Key, TextAnchor.MiddleRight, 1f, -Gutter);
                CreateText(rowRect, "Action", actionFont, textMaterial, ActionSize, Labels[i].Action, TextAnchor.MiddleLeft,
                    0f, Gutter);
                prompts.rows[i] = row;
                prompts.fadeAt[i] = float.PositiveInfinity;
            }

            prompts.input = input;
            prompts.dismissed = dismissed;
            prompts.aimOrigin = input.ScreenAimPoint;
            return prompts;
        }

        private static void CreateText(RectTransform row, string name, Font font, Material material, int size,
            string content, TextAnchor alignment, float pivotX, float x)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.gameObject.layer = row.gameObject.layer;
            text.transform.SetParent(row, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.pivot = new Vector2(pivotX, .5f);
            text.rectTransform.anchoredPosition = new Vector2(x, 0f);
            text.rectTransform.sizeDelta = new Vector2(RowWidth / 2f - Gutter, RowHeight);
            text.font = font;
            text.material = material;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = content;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            var dismissing = dismissed();
            var showing = false;
            for (var i = 0; i < rows.Length; i++)
            {
                if (float.IsPositiveInfinity(fadeAt[i]) && (dismissing || Used((Prompt)i))) fadeAt[i] = now;
                rows[i].alpha = 1f - Mathf.Clamp01((now - fadeAt[i]) / FadeSeconds);
                showing |= rows[i].alpha > 0f;
            }
            if (!showing) Destroy(gameObject);
        }

        private bool Used(Prompt prompt) => prompt switch
        {
            Prompt.Thrust => input.Thrust != 0f,
            Prompt.Strafe => input.Strafe != 0f,
            Prompt.Aim => (input.ScreenAimPoint - aimOrigin).sqrMagnitude > AimSlackPixels * AimSlackPixels,
            Prompt.Fire => input.PrimaryFire || input.SecondaryFire,
            Prompt.Boost => input.BoostDown,
            _ => throw new ArgumentOutOfRangeException(nameof(prompt), prompt, null)
        };
    }
}
