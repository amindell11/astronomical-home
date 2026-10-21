using System;
using System.Linq;
using Game.Player;
using Game.Runs;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens
{
    internal static class ScreenWidgets
    {
        private static readonly Color Ink = new(0.9f, 0.92f, 0.93f);
        private static readonly Color Muted = new(0.59f, 0.67f, 0.73f);
        private static readonly Color HullColor = new(0.94f, 0.64f, 0.5f);

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

        public static void Show(Transform root, string title, IRunTally tally, int sectorsCleared,
            DamageLedger ledger, Scoreboard board, Action onContinue)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var dim = Box(root, "Dim");
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var panel = Box(dim, "Panel");
            panel.sizeDelta = new Vector2(520f, 0f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.07f, 0.105f, 0.96f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 26, 24);
            layout.spacing = 18f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(panel, "Title", title, font, 28, Ink, TextAnchor.MiddleCenter);
            AddText(panel, "Tally",
                $"{sectorsCleared} {(sectorsCleared == 1 ? "sector" : "sectors")} cleared   ·   {tally.Kills} kills   ·   {RunTally.FormatSeconds(tally.SecondsSurvived)}",
                font, 17, Muted, TextAnchor.MiddleCenter);
            Divider(panel);
            AddDamage(panel, font, ledger);
            Divider(panel);
            AddFooter(panel, font, board, onContinue);
        }

        private static void AddDamage(Transform panel, Font font, DamageLedger ledger)
        {
            var shield = ledger.Rows.Sum(row => row.Shield);
            var hull = ledger.Rows.Sum(row => row.Hull);
            var totals = Box(panel, "DamageTotals");
            var layout = totals.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            AddText(totals, "Shield", $"<size=14>SHIELD ABSORBED</size>\n{shield:0.#} <size=16>pts</size>",
                font, 26, Muted);
            AddText(totals, "Hull", $"<size=14>HULL DAMAGE</size>\n{hull:0.#} <size=16>pts</size>",
                font, 26, hull > 0f ? HullColor : Ink);

            if (hull == 0f)
            {
                AddText(panel, "NoHullDamage", "No hull damage taken", font, 18, Ink, TextAnchor.MiddleCenter);
                return;
            }

            var slices = ledger.Rows.GroupBy(row => row.Kind)
                .Select(group => (Kind: group.Key, Amount: group.Sum(row => row.Hull)))
                .Where(slice => slice.Amount > 0f)
                .OrderByDescending(slice => slice.Amount).ThenBy(slice => slice.Kind).ToArray();
            var breakdown = Box(panel, "HullBreakdown");
            var rowLayout = breakdown.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 30f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;

            var chart = Box(breakdown, "Pie").gameObject;
            var chartSize = chart.AddComponent<LayoutElement>();
            chartSize.minWidth = chartSize.preferredWidth = 132f;
            chartSize.minHeight = chartSize.preferredHeight = 132f;
            chart.AddComponent<HullDamagePie>().Initialize(slices);

            var legend = Box(breakdown, "Legend");
            var legendLayout = legend.gameObject.AddComponent<VerticalLayoutGroup>();
            legendLayout.spacing = 7f;
            legendLayout.childControlWidth = legendLayout.childControlHeight = true;
            legendLayout.childForceExpandWidth = true;
            legendLayout.childForceExpandHeight = false;
            legend.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            AddText(legend, "Heading", "HULL DAMAGE BY SOURCE", font, 13, Muted);
            foreach (var slice in slices)
            {
                var row = Box(legend, slice.Kind.ToString());
                var cells = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                cells.childControlWidth = cells.childControlHeight = true;
                cells.childForceExpandWidth = true;
                cells.childForceExpandHeight = false;
                var color = ColorUtility.ToHtmlStringRGB(HullDamagePie.ColorFor(slice.Kind));
                AddText(row, "Weapon", $"<color=#{color}>●</color>  {HullDamagePie.LabelFor(slice.Kind)}", font, 16, Ink);
                AddText(row, "Points", $"{slice.Amount:0.#} pts", font, 16, Ink, TextAnchor.MiddleRight);
            }
        }

        private static void AddFooter(Transform parent, Font font, Scoreboard board, Action onContinue)
        {
            var footer = Box(parent, "Footer");
            var layout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var rank = AddText(footer, "Rank", board.Rank is { } place ? $"Rank {place} of {board.Rows.Count}" : "Not ranked",
                font, 14, Muted);
            rank.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var button = Box(footer, "Continue").gameObject;
            button.AddComponent<Image>().color = new Color(0.12f, 0.18f, 0.23f);
            button.AddComponent<Outline>().effectColor = new Color(0.48f, 0.59f, 0.65f);
            var size = button.AddComponent<LayoutElement>();
            size.preferredWidth = 144f;
            size.preferredHeight = 38f;
            var label = AddText(button.transform, "Label", "CONTINUE", font, 16, Ink, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            button.AddComponent<Button>().onClick.AddListener(() => onContinue?.Invoke());
        }

        private static void Divider(Transform parent)
        {
            var line = Box(parent, "Divider").gameObject;
            line.AddComponent<Image>().color = new Color(0.23f, 0.32f, 0.39f, 0.5f);
            line.AddComponent<LayoutElement>().preferredHeight = 1f;
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

        private static Text AddText(Transform parent, string name, string content, Font font,
            int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Box(parent, name).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
