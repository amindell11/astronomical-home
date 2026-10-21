using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Player;
using Game.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens
{
    internal static class ScreenWidgets
    {
        internal const int BoardRows = 5;

        internal static readonly Color DetailColor = new(0.8f, 0.83f, 0.88f);

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

        internal static string RankLine(Scoreboard board) =>
            board.Rank is { } rank ? $"RANK {rank} OF {board.Rows.Count}" : "NOT RANKED";

        // One text per column, each a header and a line per row, so rows line up across columns.
        public static void AddScoreboard(Transform parent, Font font, Scoreboard board)
        {
            AddText(parent, "Rank", RankLine(board), font, 24, Color.white, FontStyle.Bold);
            if (board.Rows.Count == 0) return;

            var go = new GameObject("Board", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var columns = go.AddComponent<HorizontalLayoutGroup>();
            columns.spacing = 28f;
            columns.childControlWidth = true;
            columns.childControlHeight = true;
            columns.childForceExpandWidth = false;
            columns.childForceExpandHeight = false;

            var top = board.Rows.Take(BoardRows).ToList();
            AddColumn(go.transform, "Place", "#", top.Select((_, i) => $"{i + 1}."), font);
            AddColumn(go.transform, "Sectors", "SECTORS", top.Select(row => $"{row.SectorsCleared}"), font);
            AddColumn(go.transform, "Kills", "KILLS", top.Select(row => $"{row.Kills}"), font);
            AddColumn(go.transform, "Time", "TIME", top.Select(row => RunTally.FormatSeconds(row.SecondsSurvived)), font);
            AddColumn(go.transform, "Date", "DATE",
                top.Select(row => row.EndedUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), font);
        }

        private static void AddColumn(Transform board, string name, string header, IEnumerable<string> cells, Font font) =>
            AddText(board, name, header + "\n" + string.Join("\n", cells), font, 17, DetailColor);

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
