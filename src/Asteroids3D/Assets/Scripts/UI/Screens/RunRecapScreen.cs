using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Player;
using Game.Runs;
using UI.Recap;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens
{
    /// <summary>
    /// The recap's uGUI: the sector or run card and, for a run outcome, its banner, built at rest for a
    /// <see cref="RecapOutcomeContext"/>. It draws whatever <see cref="RecapState"/> it is handed and
    /// drives no time of its own; a <see cref="RecapPresenter"/> applies the states.
    /// </summary>
    public sealed class RunRecapScreen : MonoBehaviour
    {
        internal const string SilhouetteFolder = "RunRecap/Ships";
        private static readonly Color Ink = new(.93f, .94f, .93f);
        private static readonly Color Muted = new(.70f, .74f, .81f);
        private static readonly Color Line = new(.84f, .87f, .93f, .25f);
        private static readonly Color Paint = new(.91f, .91f, .85f);
        private static readonly Color Success = new(.56f, .83f, .67f);
        private static readonly Color Loss = new(.91f, .58f, .58f);
        private static readonly Color Backing = new(.047f, .078f, .153f, .9f);
        private Font font;
        private CanvasGroup breakdown;
        private Button continueButton;
        private GameObject banner;
        private CanvasGroup bannerGroup;
        private GameObject prompt;
        private bool interactable;

        public bool Continued { get; private set; }

        private void Awake() => font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RunRecapScreen Create(Transform parent, RecapOutcomeContext context)
        {
            var go = new GameObject("RunRecapScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
            var screen = go.AddComponent<RunRecapScreen>();
            if (context.Outcome == RecapOutcome.Extraction) screen.BuildSector(context);
            else screen.BuildRun(context);
            return screen;
        }

        public void Apply(RecapState state)
        {
            breakdown.alpha = state.BreakdownAlpha;
            breakdown.interactable = breakdown.blocksRaycasts = state.BreakdownInteractable;
            if (state.BreakdownInteractable && !interactable) continueButton.Select();
            interactable = state.BreakdownInteractable;
            if (!banner) return;
            bannerGroup.alpha = state.BannerAlpha;
            prompt.SetActive(state.BannerPrompt);
            banner.SetActive(state.BannerAlpha > 0f || state.BannerPrompt);
        }

        private void BuildSector(RecapOutcomeContext context)
        {
            var panel = BuildPanel("EXTRACTED", Success, 338f, 380f);
            Label(panel, "Context", $"SECTOR {context.Sector:00} COMPLETE", 28, 80, 282, 20, 11, Muted);
            AddTrophies(panel, context.Kills, 110f, 86f);
            Rule(panel, 260f);
            Label(panel, "TimeLabel", "COMPLETION TIME", 28, 273, 190, 28, 11, Muted);
            Label(panel, "Time", RunTally.FormatSeconds(context.CompletionSeconds), 214, 267, 96, 34, 24, Ink,
                TextAnchor.MiddleRight);
            continueButton = Continue(panel, "NEXT SECTOR", 324f);
            breakdown = panel.gameObject.AddComponent<CanvasGroup>();
        }

        private void BuildRun(RecapOutcomeContext context)
        {
            var victory = context.Outcome == RecapOutcome.Victory;
            var outcomeColor = victory ? Success : Loss;
            var title = victory ? "VICTORY" : "SHIP LOST";
            var panel = BuildPanel(title, outcomeColor, 480f, 576f, true);
            AddTrophies(panel, context.Kills, 82f, 58f, true);
            Label(panel, "Progress", $"{context.SectorsCleared} SECTORS CLEARED", 28, 202, 424, 20, 11, Muted);
            AddRoute(panel, context.SectorsCleared, context.SectorCount, 232f);
            Rule(panel, 274f);
            AddScoreboard(panel, context.Board, outcomeColor);
            continueButton = Continue(panel, "RETURN TO HANGAR", 510f);
            breakdown = panel.gameObject.AddComponent<CanvasGroup>();
            breakdown.alpha = 0f;
            breakdown.interactable = false;
            breakdown.blocksRaycasts = false;
            BuildBanner(title, outcomeColor);
        }

        private void BuildBanner(string title, Color outcomeColor)
        {
            var intro = Box(transform, "OutcomeIntro", 0, 0, 820, 196);
            intro.anchorMin = intro.anchorMax = intro.pivot = new Vector2(.5f, .5f);
            Fill(intro, new Color(Backing.r, Backing.g, Backing.b, .72f));
            var corners = new Color(1f, 1f, 1f, .65f);
            Fill(Box(intro, "TopLeftHorizontal", 0, 0, 18, 1), corners);
            Fill(Box(intro, "TopLeftVertical", 0, 0, 1, 18), corners);
            Fill(Box(intro, "TopRightHorizontal", 802, 0, 18, 1), corners);
            Fill(Box(intro, "TopRightVertical", 819, 0, 1, 18), corners);
            Fill(Box(intro, "BottomLeftHorizontal", 0, 195, 18, 1), corners);
            Fill(Box(intro, "BottomLeftVertical", 0, 178, 1, 18), corners);
            Fill(Box(intro, "BottomRightHorizontal", 802, 195, 18, 1), corners);
            Fill(Box(intro, "BottomRightVertical", 819, 178, 1, 18), corners);
            var headline = Label(intro, "Outcome", title, 0, 0, 764, 160, 112, outcomeColor,
                TextAnchor.MiddleCenter);
            headline.rectTransform.anchorMin = headline.rectTransform.anchorMax = new Vector2(.5f, .5f);
            headline.rectTransform.pivot = new Vector2(.5f, .5f);
            bannerGroup = intro.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            prompt = Label(intro, "ContinueHint", "PRESS ANY KEY TO CONTINUE", 28, 164, 764, 20, 11, Muted,
                TextAnchor.MiddleCenter).gameObject;
            prompt.SetActive(false);
            banner = intro.gameObject;
        }

        private RectTransform BuildPanel(string title, Color outcomeColor, float width, float height,
            bool centered = false)
        {
            var blocker = Box(transform, "InputBlocker", 0, 0, 0, 0);
            blocker.anchorMin = Vector2.zero;
            blocker.anchorMax = Vector2.one;
            blocker.offsetMin = blocker.offsetMax = Vector2.zero;
            blocker.gameObject.AddComponent<Image>().color = Color.clear;
            var group = Box(transform, "Panels", 0, 0, width, height);
            group.anchorMin = group.anchorMax = new Vector2(centered ? .5f : .07f, .5f);
            group.pivot = new Vector2(centered ? .5f : 0f, .5f);
            var panel = Box(group, "Recap", 0, 0, width, height);
            Fill(panel, Backing);
            Fill(Box(panel, "CornerTop", 0, 0, 17, 1), Line);
            Fill(Box(panel, "CornerLeft", 0, 0, 1, 17), Line);
            Label(panel, "Outcome", title, 28, 27, width - 56, 48, 40, outcomeColor);
            return panel;
        }

        private void AddTrophies(RectTransform panel, IReadOnlyList<string> kills, float top, float height,
            bool compact = false)
        {
            var width = panel.rect.width - 56f;
            var columns = compact ? 9 : 5;
            var rowStep = compact ? 30f : 46f;
            var cellWidth = compact ? 32f : 50f;
            var cellHeight = compact ? 28f : 40f;
            var iconSize = compact ? 22f : 34f;
            var columnStep = (width - cellWidth) / (columns - 1);
            Label(panel, "Kills", $"{kills.Count} CONFIRMED KILLS", 28, top, width, 24, 13, Muted);
            var detail = Label(panel, "ShipName", "", 28, top + height + 38, width - 144, 18, 11, Muted);
            var viewport = Box(panel, "Trophies", 28, top + 36, width, height);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Box(viewport, "Content", 0, 0, width,
                Mathf.Max(0, Mathf.Ceil(kills.Count / (float)columns) * rowStep - rowStep + cellHeight));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = content.sizeDelta.y > height;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            if (scroll.vertical)
                Label(panel, "ScrollHint", "SCROLL TO VIEW ALL", width - 116, top + height + 38, 144, 18, 10, Muted,
                    TextAnchor.MiddleRight);
            var textures = new Dictionary<string, Texture2D>();
            for (var i = 0; i < kills.Count; i++)
            {
                var chassis = kills[i];
                if (!textures.TryGetValue(chassis, out var texture))
                {
                    texture = Resources.Load<Texture2D>(SilhouetteFolder + "/" + chassis);
                    if (!texture) throw new InvalidOperationException($"No recap silhouette for '{chassis}'.");
                    textures.Add(chassis, texture);
                }
                var trophy = Box(content, $"Kill{i + 1}_{chassis}", i % columns * columnStep,
                    i / columns * rowStep, cellWidth, cellHeight);
                trophy.gameObject.AddComponent<Image>().color = Color.clear;
                var icon = Box(trophy, "Silhouette", (cellWidth - iconSize) / 2, 3, iconSize, iconSize)
                    .gameObject.AddComponent<RawImage>();
                icon.texture = texture;
                icon.color = Paint;
                icon.raycastTarget = false;
                trophy.gameObject.AddComponent<Button>().onClick.AddListener(() => detail.text = chassis);
            }
        }

        private void AddRoute(RectTransform panel, int cleared, int count, float top)
        {
            var spacing = count > 1 ? (panel.rect.width - 82f) / (count - 1) : 0f;
            for (var i = 0; i < count; i++)
            {
                var x = 28 + i * spacing;
                var reached = i == cleared;
                var color = reached ? new Color(.93f, .73f, .62f) : i < cleared ? Ink : Muted;
                if (i > 0) Fill(Box(panel, "Route", x - spacing + 36, top + 13, spacing - 46, 1), Line);
                var node = Box(panel, "Sector" + (i + 1), x, top, 26, 26);
                var marker = node.gameObject.AddComponent<RecapSectorMarker>();
                marker.Reached = reached;
                marker.color = reached ? new Color(.89f, .71f, .58f, .65f) : new Color(.65f, .73f, .81f, .13f);
                marker.raycastTarget = false;
                Label(node, "Number", (i + 1).ToString("00"), 0, 0, 26, 26, 11, color, TextAnchor.MiddleCenter);
            }
        }

        private void AddScoreboard(Transform parent, Scoreboard board, Color outcomeColor)
        {
            var panel = Box(parent, "Scoreboard", 28, 286, 424, 202);
            Label(panel, "Heading", "SCOREBOARD", 0, 0, 132, 20, 13, Ink);
            var rank = board.Rank.HasValue ? $"{board.Rank} of {board.Rows.Count}" : "Not recorded";
            Label(panel, "Rank", "Your best runs · " + rank, 132, 0, 292, 20, 11, Muted, TextAnchor.MiddleRight);
            ScoreCells(panel, "Columns", 26, "Run", "Sectors", "Kills", "Time", Muted, false);
            var visible = Math.Min(5, board.Rows.Count);
            for (var i = 0; i < visible; i++)
            {
                var index = i == 4 && board.Rank > 5 ? board.Rank.Value - 1 : i;
                var row = board.Rows[index];
                var current = board.Rank == index + 1;
                var y = 52 + i * 30f;
                Fill(Box(panel, "Divider", 0, y, 424, 1), Line);
                if (current)
                    Fill(Box(panel, "CurrentRun", 0, y, 424, 30),
                        new Color(outcomeColor.r, outcomeColor.g, outcomeColor.b, .07f));
                ScoreCells(panel, "Row" + (index + 1), y + 3,
                    $"{index + 1}   " + (current ? "This run" : row.EndedUtc.ToLocalTime().ToString("MMM d", CultureInfo.InvariantCulture)),
                    row.SectorsCleared.ToString(), row.Kills.ToString(), RunTally.FormatSeconds(row.SecondsSurvived),
                    current ? outcomeColor : Ink, true);
            }
        }

        private void ScoreCells(RectTransform panel, string name, float y, string run, string sectors,
            string kills, string time, Color color, bool numeric)
        {
            var row = Box(panel, name, 12, y, 400, 24);
            Label(row, "Run", run, 0, 0, 138, 24, 11, color);
            Label(row, "Sectors", sectors, 150, 0, 98, 24, 11, numeric ? Ink : color, TextAnchor.MiddleRight);
            Label(row, "Kills", kills, 260, 0, 76, 24, 11, numeric ? Ink : color, TextAnchor.MiddleRight);
            Label(row, "Time", time, 350, 0, 50, 24, 11, numeric ? Ink : color, TextAnchor.MiddleRight);
        }

        private Button Continue(RectTransform parent, string caption, float y)
        {
            var rect = Box(parent, "Continue", 28, y, 216, 38);
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            Fill(Box(rect, "Top", 0, 0, 216, 1), Line);
            Fill(Box(rect, "Bottom", 0, 37, 216, 1), Line);
            Fill(Box(rect, "Left", 0, 0, 1, 38), Line);
            Fill(Box(rect, "Right", 215, 0, 1, 38), Line);
            Label(rect, "Label", caption, 14, 0, 182, 38, 11, Ink);
            Label(rect, "Arrow", "›", 190, 0, 20, 38, 20, Ink);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                button.interactable = false;
                Continued = true;
            });
            return button;
        }

        private static void Rule(RectTransform parent, float y) =>
            Fill(Box(parent, "Divider", 28, y, parent.rect.width - 56, 1), Line);

        private static void Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private Text Label(Transform parent, string name, string value, float x, float y, float width,
            float height, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Box(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }
    }
}
