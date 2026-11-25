using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Windows
{
    internal static class WindowKit
    {
        internal static readonly Color Ink = Color.white;
        internal static readonly Color Dim = new(1f, 1f, 1f, .58f);
        internal static readonly Color Line = new(1f, 1f, 1f, .42f);
        internal static readonly Color Bar = new(1f, 1f, 1f, .2f);
        internal static readonly Color Body = new(.004f, .02f, .04f, .72f);
        internal static readonly Color Good = new(.49f, .94f, .71f);
        internal static readonly Color Threat = new(1f, .49f, .42f);

        // The canopy adds ink as light past gamma and halo, so a fill there reads several times brighter.
        private const float CanopyFill = .2f;

        internal static Color Lit(ScreenLayers layers, Color fill) =>
            layers.Canopy ? new Color(fill.r, fill.g, fill.b, fill.a * CanopyFill) : fill;

        internal const float StrongFade = .58f;
        // The sector jump's streaks are part of the scene, so its card barely fades them.
        internal const float LightFade = .18f;

        /// <summary>Fades the game view behind the windows and stops clicks reaching it.</summary>
        internal static Image Fade(Transform parent, float strength)
        {
            var rect = ScreenKit.Box(parent, "Fade", 0f, 0f, 0f, 0f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.004f, .012f, .03f, strength);
            return image;
        }

        // Opaque to hide window ink behind it; black through the canopy, where its shade does the darkening.
        internal static Color OpaqueBody(ScreenLayers layers) =>
            layers.Canopy ? Color.black : new Color(Body.r, Body.g, Body.b, 1f);

        internal static ChamferedShape Shape(RectTransform rect, Color color, float chamfer, float stroke,
            bool squareBottom = false)
        {
            var shape = rect.gameObject.AddComponent<ChamferedShape>();
            shape.color = color;
            shape.Chamfer = chamfer;
            shape.Stroke = stroke;
            shape.SquareBottom = squareBottom;
            shape.raycastTarget = false;
            return shape;
        }

        internal static Text Label(Transform parent, string name, string value, float x, float y, float width,
            float height, int size, Color color, Font font, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = ScreenKit.Label(ScreenKit.Box(parent, name, x, y, width, height), value, size, color, font,
                alignment);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        /// <summary>A dim caption with its value set in ink straight after it.</summary>
        internal static Text Captioned(Transform parent, string name, string caption, string value, float x, float y,
            float width, int size, Font font)
        {
            var label = Label(parent, name + "Label", caption, x, y, width, 20f, size, Dim, font);
            var offset = label.preferredWidth + 8f;
            return Label(parent, name, value, x + offset, y, width - offset, 20f, size, Ink, font);
        }

        internal static void Rule(Transform parent, float x, float y, float width) =>
            ScreenKit.Fill(ScreenKit.Box(parent, "Rule", x, y, width, 1f), Line);

        /// <summary>An outlined button with its caption on the left and its key on the right.</summary>
        internal static Button KeyButton(ScreenLayers layers, RectTransform rect, string caption, string key, Font font,
            Action press)
        {
            var width = rect.sizeDelta.x;
            var height = rect.sizeDelta.y;
            var back = ScreenKit.Fill(ScreenKit.Box(rect, "Back", 0f, 0f, width, height),
                layers.Canopy ? Color.black : Body);
            if (layers.Canopy) WindowShade.Create(layers, back, 0f);
            Shape(ScreenKit.Box(rect, "Frame", 0f, 0f, width, height), Ink, 0f, 1.5f);
            Label(rect, "Label", caption, 14f, 0f, width - 28f, height, 15, Ink, font);
            Label(rect, "Key", key, 14f, 0f, width - 28f, height, 15, Dim, font, TextAnchor.MiddleRight);
            return ScreenKit.Button(rect, press, Lit(layers, ScreenKit.Hover));
        }
    }
}
