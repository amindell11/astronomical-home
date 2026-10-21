using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens
{
    internal static class ScreenWidgets
    {
        private static readonly Color Ink = new(0.9f, 0.93f, 0.95f);
        private static readonly Color Muted = new(0.6f, 0.69f, 0.75f);
        private static readonly Color Line = new(0.65f, 0.75f, 0.81f, 0.2f);

        public static T CreateOverlay<T>(Transform parent, string name) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go.AddComponent<T>();
        }

        public static void Show(Transform root, string title, int kills, int sectorsCleared, Action onContinue)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var dim = Box(root, "Dim");
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.52f);

            var panel = Box(dim, "Panel");
            panel.sizeDelta = new Vector2(460f, 0f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.08f, 0.12f, 0.97f);
            panel.gameObject.AddComponent<Outline>().effectColor = Line;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 30, 28);
            layout.spacing = 28f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(panel, "Title", title, font, 20, Muted);

            var stats = Box(panel, "Stats");
            var statsLayout = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
            statsLayout.spacing = 18f;
            statsLayout.childAlignment = TextAnchor.MiddleCenter;
            statsLayout.childControlWidth = statsLayout.childControlHeight = true;
            statsLayout.childForceExpandWidth = statsLayout.childForceExpandHeight = false;
            AddMetric(stats, "Kills", kills, "KILLS", font);
            var divider = Box(stats, "Divider").gameObject;
            divider.AddComponent<Image>().color = Line;
            var dividerSize = divider.AddComponent<LayoutElement>();
            dividerSize.minWidth = dividerSize.preferredWidth = 1f;
            dividerSize.preferredHeight = 76f;
            AddMetric(stats, "Sectors", sectorsCleared, "SECTORS CLEARED", font);

            var footer = Box(panel, "Footer");
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = footerLayout.childForceExpandHeight = false;
            var button = Box(footer, "Continue").gameObject;
            button.AddComponent<Image>().color = new Color(0.09f, 0.14f, 0.18f);
            button.AddComponent<Outline>().effectColor = new Color(0.6f, 0.7f, 0.77f, 0.55f);
            var buttonSize = button.AddComponent<LayoutElement>();
            buttonSize.preferredWidth = 148f;
            buttonSize.preferredHeight = 40f;
            var label = AddText(button.transform, "Label", "CONTINUE", font, 14, Ink);
            Stretch(label.rectTransform);
            button.AddComponent<Button>().onClick.AddListener(() => onContinue?.Invoke());
        }

        private static void AddMetric(Transform parent, string name, int value, string label, Font font)
        {
            var metric = Box(parent, name);
            var layout = metric.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var size = metric.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 0f;
            size.flexibleWidth = 1f;
            AddText(metric, "Value", value.ToString(), font, 68, Ink);
            AddText(metric, "Label", label, font, 13, Muted);
        }

        private static RectTransform Box(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Text AddText(Transform parent, string name, string content, Font font, int size, Color color)
        {
            var text = Box(parent, name).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
