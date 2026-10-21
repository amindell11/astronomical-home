using System;
using Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens
{
    internal static class ScreenWidgets
    {
        public static T CreateOverlay<T>(Transform parent, string name) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            go.AddComponent<GraphicRaycaster>();
            return go.AddComponent<T>();
        }

        public static Transform AddPanel(Transform root)
        {
            var dim = AddStretchedImage(root, "Dim", new Color(0f, 0f, 0f, 0.65f));

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(dim.transform, false);
            panel.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.9f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 24, 24);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel.transform;
        }

        public static Text AddText(Transform parent, string name, string content, Font font,
            int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        internal static string TallyBlock(IRunTally tally) =>
            $"Kills: {tally.Kills}\nTime survived: {RunTally.FormatSeconds(tally.SecondsSurvived)}";

        public static void AddContinueButton(Transform parent, Font font, Action onContinue)
        {
            var go = new GameObject("Continue", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(0.20f, 0.55f, 0.95f, 1f);
            var size = go.AddComponent<LayoutElement>();
            size.preferredWidth = 220f;
            size.preferredHeight = 44f;
            var label = AddText(go.transform, "Label", "CONTINUE", font, 20, Color.white, FontStyle.Bold);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Button>().onClick.AddListener(() => onContinue?.Invoke());
        }

        private static GameObject AddStretchedImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color;

            // Center anchor for children laid out inside the stretched dim.
            var center = go.AddComponent<VerticalLayoutGroup>();
            center.childAlignment = TextAnchor.MiddleCenter;
            center.childControlWidth = false;
            center.childControlHeight = false;
            center.childForceExpandWidth = false;
            center.childForceExpandHeight = false;
            return go;
        }
    }
}
