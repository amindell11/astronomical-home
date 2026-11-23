using System;
using System.Collections.Generic;
using Game.Player.Recorders;
using Game.Runs;
using Ships.Presentation;
using UI.Recap;
using UI.Screens.Windows;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens
{
    /// <summary>
    /// The recap's uGUI, built at rest for a <see cref="RecapOutcomeContext"/>: the sector window, or for
    /// a run outcome its banner, the Flight Recorder window and the scoreboard window that opens in
    /// front of it on request. It draws whatever <see cref="RecapState"/> it is handed, so playback or
    /// animation inspection drives every window but the scoreboard, whose opening is the player's own
    /// and runs on the unscaled clock here.
    /// The look was settled on https://github.com/amindell11/astronomical-home/issues/1694
    /// </summary>
    public sealed class RunRecapScreen : MonoBehaviour
    {
        internal const string SilhouetteFolder = ShipSilhouette.Folder;
        internal const int PageSize = 4;

        private const float BoardSeconds = .9f;
        private const float SlideEnd = .3f;
        private const float RowHeight = 41f;
        private static readonly Vector2 RecorderCentred = new(340f, -240f);
        private static readonly Vector2 RecorderAside = new(167f, -47f);
        private static readonly Color Trophy = new(1f, 1f, 1f, .72f);
        private static readonly Color Cleared = new(1f, 1f, 1f, .55f);
        private static readonly Color Ahead = new(1f, 1f, 1f, .13f);
        private static readonly Color OnCleared = new(.004f, .02f, .04f);

        private readonly List<GameObject> rows = new();
        private ScreenFonts fonts;
        private ScreenLayers layers;
        private bool sector;
        private Image fade;
        private float fadeStrength;
        private CanvasGroup breakdown;
        private CanvasGroup actions;
        private ScreenWindow recap;
        private ScreenWindow board;
        private Button continueButton;
        private ScreenWindow banner;
        private GameObject prompt;
        private bool interactable;
        private InputAction boardKey;
        private float boardProgress;
        private int page;
        private int pageCount;
        private Text pageLabel;
        private Button previousPage;
        private Button nextPage;
        private Text previousLabel;
        private Text nextLabel;

        public bool Continued { get; private set; }

        /// <summary>Whether the player has the scoreboard window open; only a run outcome has one.</summary>
        public bool ScoreboardShown { get; private set; }

        private void Awake() => fonts = ScreenFonts.Load();

        private void OnDestroy() => boardKey?.Dispose();

        public static RunRecapScreen Create(Transform parent, RecapOutcomeContext context, ScreenCameras cameras)
        {
            var go = new GameObject("RunRecapScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layers = ScreenLayers.Create(go, cameras, context.Outcome == RecapOutcome.Extraction
                ? CanvasScaler.ScreenMatchMode.MatchWidthOrHeight : CanvasScaler.ScreenMatchMode.Expand);
            var screen = go.AddComponent<RunRecapScreen>();
            screen.layers = layers;
            screen.sector = context.Outcome == RecapOutcome.Extraction;
            if (screen.sector) screen.BuildSector(context);
            else screen.BuildRun(context);
            return screen;
        }

        public void Apply(RecapState state)
        {
            fade.color = new Color(fade.color.r, fade.color.g, fade.color.b, fadeStrength * FadeLevel(state));
            breakdown.alpha = state.BreakdownAlpha > 0f ? 1f : 0f;
            breakdown.interactable = breakdown.blocksRaycasts = state.BreakdownInteractable;
            recap.Open = state.BreakdownAlpha;
            actions.alpha = state.BreakdownAlpha >= 1f ? 1f : 0f;
            if (state.BreakdownInteractable && !interactable) continueButton.Select();
            interactable = state.BreakdownInteractable;
            if (!banner) return;
            banner.Alpha = state.BannerAlpha > 0f ? 1f : 0f;
            banner.Open = state.BannerAlpha;
            prompt.SetActive(state.BannerPrompt);
            banner.gameObject.SetActive(state.BannerAlpha > 0f || state.BannerPrompt);
        }

        /// <summary>Opens or closes the scoreboard window; the Flight Recorder slides aside for it.</summary>
        public void ToggleScoreboard()
        {
            if (!board) throw new InvalidOperationException("A sector recap has no scoreboard.");
            if (!interactable) return;
            ScoreboardShown = !ScoreboardShown;
        }

        private void Update()
        {
            if (!board) return;
            var target = ScoreboardShown ? 1f : 0f;
            if (boardProgress == target) return;
            boardProgress = Mathf.MoveTowards(boardProgress, target, Time.unscaledDeltaTime / BoardSeconds);
            PlaceBoard();
        }

        private void PlaceBoard()
        {
            var slide = Mathf.SmoothStep(0f, 1f, boardProgress / SlideEnd);
            ((RectTransform)recap.transform).anchoredPosition = Vector2.Lerp(RecorderCentred, RecorderAside, slide);
            board.Open = Mathf.InverseLerp(SlideEnd * .8f, 1f, boardProgress);
        }

        // The view stays faded from the banner through the breakdown, so the gap between them does not flash.
        private float FadeLevel(RecapState state) => state.Phase switch
        {
            RecapPhase.HudFade => 0f,
            RecapPhase.BannerIn => state.BannerAlpha,
            RecapPhase.BreakdownIn when sector => state.BreakdownAlpha,
            RecapPhase.BreakdownOut => state.BreakdownAlpha,
            _ => 1f,
        };

        private void BuildSector(RecapOutcomeContext context)
        {
            const float width = 373f;
            const float height = 293f;
            var group = BuildStage(WindowKit.LightFade, width, height + 58f, new Vector2(.07f, .5f),
                new Vector2(0f, .5f));
            recap = ScreenWindow.Create(group, layers, "Recap", 0f, 0f, width, height, $"SECTOR {context.Sector:00}",
                fonts.Label);
            var body = recap.Body;
            WindowKit.Label(body, "Outcome", "EXTRACTED", 20f, 12f, width - 40f, 50f, 40, WindowKit.Good,
                fonts.Heading);
            WindowKit.Rule(body, 20f, 75f, width - 40f);
            Stat(body, "Kills", "KILLS", context.Kills.Count.ToString(), 20f, 86f);
            Stat(body, "Time", "TIME", RunTally.FormatSeconds(context.CompletionSeconds), 120f, 86f);
            AddTrophies(body, context.Kills, 20f, 152f, 33f, 12f);
            actions = Actions(group);
            continueButton = Continue(ScreenKit.Box(actions.transform, "Continue", 0f, height + 17f, 253f, 41f),
                "NEXT SECTOR");
        }

        private void BuildRun(RecapOutcomeContext context)
        {
            var victory = context.Outcome == RecapOutcome.Victory;
            var outcomeColor = victory ? WindowKit.Good : WindowKit.Threat;
            var title = victory ? "VICTORY" : "SHIP LOST";
            var centre = new Vector2(.5f, .5f);
            var group = BuildStage(WindowKit.StrongFade, 1280f, 720f, centre, centre);

            recap = ScreenWindow.Create(group, layers, "Recap", RecorderCentred.x, -RecorderCentred.y, 600f, 240f,
                "FLIGHT RECORDER", fonts.Label);
            var body = recap.Body;
            WindowKit.Label(body, "Outcome", title, 20f, 10f, 560f, 56f, 47, outcomeColor, fonts.Heading);
            WindowKit.Captioned(body, "Kills", "KILLS", context.Kills.Count.ToString(), 20f, 83f, 271f, 14,
                fonts.Label);
            AddTrophies(body, context.Kills, 20f, 107f, 31f, 9f);
            WindowKit.Captioned(body, "Progress", "SECTORS", $"{context.SectorsCleared} / {context.SectorCount}",
                347f, 83f, 233f, 14, fonts.Label);
            AddRoute(body, context.SectorsCleared, context.SectorCount, 347f, 118f, 233f);

            BuildScoreboard(group, context.Board, outcomeColor);
            actions = Actions(group);
            WindowKit.KeyButton(layers, ScreenKit.Box(actions.transform, "ScoreboardButton", 340f, 593f, 260f, 41f),
                "SCOREBOARD", "TAB", fonts.Label, ToggleScoreboard);
            continueButton = Continue(ScreenKit.Box(actions.transform, "Continue", 620f, 593f, 320f, 41f),
                "RETURN TO HANGAR");
            boardKey = new InputAction("Scoreboard", InputActionType.Button, "<Keyboard>/tab");
            boardKey.AddBinding("<Gamepad>/buttonNorth");
            boardKey.performed += _ => ToggleScoreboard();
            boardKey.Enable();
            BuildBanner(title, outcomeColor);
        }

        private RectTransform BuildStage(float strength, float width, float height, Vector2 anchor, Vector2 pivot)
        {
            fadeStrength = strength;
            fade = WindowKit.Fade(layers.Under, 0f);
            var group = ScreenKit.Box(transform, "Panels", 0f, 0f, width, height);
            group.anchorMin = group.anchorMax = anchor;
            group.pivot = pivot;
            breakdown = group.gameObject.AddComponent<CanvasGroup>();
            breakdown.alpha = 0f;
            breakdown.interactable = breakdown.blocksRaycasts = false;
            return group;
        }

        // Buttons sit outside the windows, so they wait for the window to finish opening.
        private static CanvasGroup Actions(RectTransform group)
        {
            var holder = ScreenKit.Box(group, "Actions", 0f, 0f, group.sizeDelta.x, group.sizeDelta.y);
            var shown = holder.gameObject.AddComponent<CanvasGroup>();
            shown.alpha = 0f;
            return shown;
        }

        private void BuildBanner(string title, Color outcomeColor)
        {
            const float width = 760f;
            const float height = 190f;
            banner = ScreenWindow.Create(transform, layers, "OutcomeIntro", 0f, 0f, width, height,
                "FLIGHT RECORDER", fonts.Label);
            var intro = (RectTransform)banner.transform;
            intro.anchorMin = intro.anchorMax = intro.pivot = new Vector2(.5f, .5f);
            intro.anchoredPosition = Vector2.zero;
            WindowKit.Label(banner.Body, "Outcome", title, 0f, 8f, width, 118f, 96, outcomeColor, fonts.Heading,
                TextAnchor.MiddleCenter);
            prompt = WindowKit.Label(banner.Body, "ContinueHint", "PRESS ANY KEY", 0f, 130f, width, 20f, 14,
                WindowKit.Dim, fonts.Label, TextAnchor.MiddleCenter).gameObject;
            prompt.SetActive(false);
            banner.Alpha = 0f;
            banner.Open = 0f;
        }

        private void Stat(RectTransform body, string name, string caption, string value, float x, float y)
        {
            WindowKit.Label(body, name + "Label", caption, x, y, 90f, 18f, 13, WindowKit.Dim, fonts.Label);
            WindowKit.Label(body, name, value, x, y + 20f, 90f, 30f, 23, WindowKit.Ink, fonts.Heading);
        }

        // Seven to a row and two rows showing; a longer list scrolls.
        private static void AddTrophies(RectTransform body, IReadOnlyList<string> kills, float x, float y,
            float icon, float gap)
        {
            const int columns = 7;
            var step = icon + gap;
            var width = columns * step - gap;
            var height = 2f * step - gap;
            var viewport = ScreenKit.Box(body, "Trophies", x, y, width, height);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = ScreenKit.Box(viewport, "Content", 0f, 0f, width,
                Mathf.Max(0f, Mathf.Ceil(kills.Count / (float)columns) * step - gap));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = content.sizeDelta.y > height;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            var textures = new Dictionary<string, Texture2D>();
            for (var i = 0; i < kills.Count; i++)
            {
                var chassis = kills[i];
                if (!textures.TryGetValue(chassis, out var texture))
                {
                    texture = ShipSilhouette.Load(chassis);
                    if (!texture) throw new InvalidOperationException($"No recap silhouette for '{chassis}'.");
                    textures.Add(chassis, texture);
                }
                var trophy = ScreenKit.Box(content, $"Kill{i + 1}_{chassis}", i % columns * step, i / columns * step,
                    icon, icon).gameObject.AddComponent<RawImage>();
                trophy.texture = texture;
                trophy.color = Trophy;
                trophy.raycastTarget = false;
            }
        }

        private void AddRoute(RectTransform body, int cleared, int count, float x, float y, float width)
        {
            const float node = 35f;
            var spacing = count > 1 ? (width - node) / (count - 1) : 0f;
            for (var i = 0; i < count; i++)
            {
                var left = x + i * spacing;
                var reached = i == cleared;
                if (i > 0)
                    ScreenKit.Fill(ScreenKit.Box(body, "Route", left - spacing + node + 9f, y + node / 2f,
                        spacing - node - 18f, 1f), i <= cleared ? WindowKit.Ink : WindowKit.Line);
                var marker = ScreenKit.Box(body, "Sector" + (i + 1), left, y, node, node).gameObject
                    .AddComponent<RecapSectorMarker>();
                marker.Reached = reached;
                marker.color = reached ? WindowKit.Threat : i < cleared ? Cleared : Ahead;
                marker.raycastTarget = false;
                WindowKit.Label(marker.transform, "Number", (i + 1).ToString("00"), 0f, 0f, node, node, 12,
                    reached ? WindowKit.Threat : i < cleared ? OnCleared : WindowKit.Dim, fonts.Heading,
                    TextAnchor.MiddleCenter);
            }
        }

        private void BuildScoreboard(RectTransform group, Scoreboard scores, Color outcomeColor)
        {
            const float width = 800f;
            const float height = 307f;
            var weaponIcons = RecapWeaponIcons.Load();
            var rank = scores.Rank.HasValue ? $"{scores.Rank} OF {scores.Rows.Count}" : "";
            board = ScreenWindow.Create(group, layers, "Scoreboard", 313f, 253f, width, height, "SCOREBOARD",
                fonts.Label, rank);
            var body = board.Body;
            var columns = ScoreCells(body, "Columns", 9f, 0f, "RANK", "SECTORS", "KILLS", "TIME", WindowKit.Dim,
                WindowKit.Dim, 14);
            WindowKit.Label(columns, "ShipHeading", "SHIP", 75f, 0f, 200f, 26f, 14, WindowKit.Dim, fonts.Label);
            WindowKit.Label(columns, "LoadoutHeading", "LOADOUT", 440f, 0f, 70f, 26f, 14, WindowKit.Dim,
                fonts.Label);
            for (var i = 0; i < scores.Rows.Count; i++)
            {
                var row = scores.Rows[i];
                var current = scores.Rank == i + 1;
                var cells = ScoreCells(body, "Row" + (i + 1), 32f + i % PageSize * RowHeight, 7f, (i + 1).ToString(),
                    row.SectorsCleared.ToString(), row.Kills.ToString(), RunTally.FormatSeconds(row.SecondsSurvived),
                    current ? outcomeColor : WindowKit.Ink, WindowKit.Ink, 17);
                ScreenKit.Fill(ScreenKit.Box(cells, "Divider", 0f, 0f, cells.sizeDelta.x, 1f), WindowKit.Line);
                if (current)
                {
                    var highlight = ScreenKit.Box(cells, "CurrentRun", 0f, 1f, cells.sizeDelta.x, RowHeight - 1f);
                    ScreenKit.Fill(highlight, new Color(outcomeColor.r, outcomeColor.g, outcomeColor.b, .2f));
                    highlight.SetAsFirstSibling();
                }
                var ship = WindowKit.Label(cells, "Ship", string.IsNullOrEmpty(row.Player.chassis)
                    ? "Ship not recorded" : row.Player.chassis, 75f, 7f, 340f, 26f, 17,
                    current ? outcomeColor : WindowKit.Ink, fonts.Label);
                Ellipsize(ship);
                var texture = string.IsNullOrEmpty(row.Player.chassis) ? null
                    : ShipSilhouette.Load(row.Player.chassis);
                if (texture)
                {
                    var icon = ScreenKit.Box(cells, "Silhouette", 41f, 8f, 25f, 25f).gameObject
                        .AddComponent<RawImage>();
                    icon.texture = texture;
                    icon.color = WindowKit.Ink;
                    icon.raycastTarget = false;
                }
                else
                    WindowKit.Label(cells, "UnavailableIcon", "?", 41f, 7f, 25f, 26f, 17, WindowKit.Dim, fonts.Label,
                        TextAnchor.MiddleCenter);
                WeaponIcon(cells, "Primary", 440f, weaponIcons.Find(row.Player.primary));
                WeaponIcon(cells, "Secondary", 472f, weaponIcons.Find(row.Player.secondary));
                rows.Add(cells.gameObject);
            }

            pageCount = Mathf.Max(1, Mathf.CeilToInt(scores.Rows.Count / (float)PageSize));
            var pager = ScreenKit.Box(body, "Pager", width - 20f - 242f, height - ScreenWindow.BarHeight - 42f, 242f,
                26f);
            previousPage = PagerButton(pager, "Previous", 0f, "‹ PREV", () => ShowPage(page - 1), out previousLabel);
            pageLabel = WindowKit.Label(pager, "Page", "", 84f, 0f, 74f, 26f, 15, WindowKit.Ink, fonts.Label,
                TextAnchor.MiddleCenter);
            nextPage = PagerButton(pager, "Next", 158f, "NEXT ›", () => ShowPage(page + 1), out nextLabel);
            pager.gameObject.SetActive(pageCount > 1);
            ShowPage(scores.Rank.HasValue ? (scores.Rank.Value - 1) / PageSize : 0);
            board.Open = 0f;
        }

        private Button PagerButton(RectTransform pager, string name, float x, string caption, Action press,
            out Text label)
        {
            var rect = ScreenKit.Box(pager, name, x, 0f, 84f, 26f);
            WindowKit.Shape(ScreenKit.Box(rect, "Frame", 0f, 0f, 84f, 26f), WindowKit.Line, 0f, 1.5f);
            label = WindowKit.Label(rect, "Label", caption, 0f, 0f, 84f, 26f, 15, WindowKit.Ink, fonts.Label,
                TextAnchor.MiddleCenter);
            return ScreenKit.Button(rect, press);
        }

        private void ShowPage(int index)
        {
            page = Mathf.Clamp(index, 0, pageCount - 1);
            for (var i = 0; i < rows.Count; i++) rows[i].SetActive(i / PageSize == page);
            pageLabel.text = $"{page + 1} / {pageCount}";
            previousPage.interactable = page > 0;
            nextPage.interactable = page < pageCount - 1;
            previousLabel.color = previousPage.interactable ? WindowKit.Ink : WindowKit.Dim;
            nextLabel.color = nextPage.interactable ? WindowKit.Ink : WindowKit.Dim;
        }

        private static void WeaponIcon(Transform parent, string name, float x, Sprite sprite)
        {
            var icon = ScreenKit.Box(parent, name, x, 8f, 24f, 24f).gameObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.enabled = sprite;
            icon.preserveAspect = true;
            icon.color = WindowKit.Ink;
            icon.raycastTarget = false;
        }

        private static void Ellipsize(Text text)
        {
            text.supportRichText = false;
            var fullName = text.text;
            var length = fullName.Length;
            while (text.preferredWidth > text.rectTransform.rect.width)
                text.text = fullName.Substring(0, --length) + "…";
        }

        private RectTransform ScoreCells(RectTransform body, string name, float y, float labelY, string rank,
            string sectors, string kills, string time, Color rankColor, Color color, int size)
        {
            var row = ScreenKit.Box(body, name, 12f, y, 776f, RowHeight);
            WindowKit.Label(row, "Rank", rank, 8f, labelY, 33f, 26f, size, rankColor, fonts.Label);
            WindowKit.Label(row, "Sectors", sectors, 520f, labelY, 60f, 26f, size, color, fonts.Label,
                TextAnchor.MiddleRight);
            WindowKit.Label(row, "Kills", kills, 600f, labelY, 60f, 26f, size, color, fonts.Label,
                TextAnchor.MiddleRight);
            WindowKit.Label(row, "Time", time, 680f, labelY, 84f, 26f, size, color, fonts.Label,
                TextAnchor.MiddleRight);
            return row;
        }

        private Button Continue(RectTransform rect, string caption)
        {
            Button button = null;
            button = WindowKit.KeyButton(layers, rect, caption, "ENTER ›", fonts.Label, () =>
            {
                button.interactable = false;
                Continued = true;
            });
            return button;
        }
    }
}
