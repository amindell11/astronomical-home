using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens
{
    internal static class ScreenKit
    {
        internal static readonly Color Ink = new(.93f, .94f, .93f);
        internal static readonly Color Muted = new(.70f, .74f, .81f);
        internal static readonly Color Line = new(.84f, .87f, .93f, .25f);
        internal static readonly Color Paint = new(.91f, .91f, .85f);
        internal static readonly Color Backing = new(.047f, .078f, .153f, .9f);
        internal static readonly Color Hover = new(1f, 1f, 1f, .08f);

        private static readonly Color DimColor = new(.047f, .078f, .153f, .55f);

        internal static void Canvas(GameObject go)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
        }

        internal static RectTransform Centered(Transform parent, string name, float width, float height, float y)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static void Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        internal static Text Label(RectTransform rect, string value, int size, Color color, Font font,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            // Truncate drops a whole line taller than its box.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        internal static void Frame(RectTransform rect)
        {
            var width = rect.sizeDelta.x;
            var height = rect.sizeDelta.y;
            Fill(Box(rect, "Top", 0, 0, width, 1), Line);
            Fill(Box(rect, "Bottom", 0, height - 1, width, 1), Line);
            Fill(Box(rect, "Left", 0, 0, 1, height), Line);
            Fill(Box(rect, "Right", width - 1, 0, 1, height), Line);
        }

        internal static void Dim(Transform parent)
        {
            var rect = (RectTransform)new GameObject("Dim", typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = DimColor;
            image.raycastTarget = true;
        }

        internal static void Button(RectTransform rect, Action onClick)
        {
            var overlay = Box(rect, "Hover", 0, 0, rect.sizeDelta.x, rect.sizeDelta.y);
            var image = overlay.gameObject.AddComponent<Image>();
            image.color = Hover;

            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.clear;
            colors.selectedColor = Color.clear;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.fadeDuration = 0f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
        }
    }
}
