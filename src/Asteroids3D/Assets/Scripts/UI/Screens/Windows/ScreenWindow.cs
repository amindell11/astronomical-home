using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Windows
{
    /// <summary>
    /// One chamfered window: a dark body, an outline and a title bar that fills to the cut corners,
    /// with its content parented under <see cref="Body"/>. It opens as a drawn line that widens,
    /// unfolds to full height, then fills its content in from the top. <see cref="Open"/> places it
    /// anywhere along that opening, so a scrubbed sequence can drive it; <see cref="Play"/> runs the
    /// opening on the unscaled clock for a screen that has no sequence.
    /// The look was settled on https://github.com/amindell11/astronomical-home/issues/1694
    /// </summary>
    public sealed class ScreenWindow : MonoBehaviour
    {
        internal const float BarHeight = 28f;
        internal const float OpenSeconds = .75f;

        private const float Chamfer = 15f;
        private const float Stroke = 2f;
        private const float WidenEnd = .25f;
        private const float UnfoldEnd = .6f;
        private const int FillSteps = 6;

        private RectTransform root;
        private RectMask2D unfold;
        private RectMask2D fill;
        private RectTransform seam;
        private float open = 1f;
        private float playedAt = float.NegativeInfinity;

        public RectTransform Body { get; private set; }

        /// <summary>0 is closed, 1 fully open with its content filled in.</summary>
        public float Open
        {
            get => open;
            set
            {
                open = Mathf.Clamp01(value);
                Place();
            }
        }

        internal static ScreenWindow Create(Transform parent, string name, float x, float y, float width,
            float height, string title, Font font, string corner = "")
        {
            var rect = ScreenKit.Box(parent, name, x, y, width, height);
            var window = rect.gameObject.AddComponent<ScreenWindow>();
            window.root = rect;
            window.unfold = rect.gameObject.AddComponent<RectMask2D>();
            WindowKit.Shape(ScreenKit.Box(rect, "Back", 0f, 0f, width, height), WindowKit.Body, Chamfer, 0f);
            WindowKit.Shape(ScreenKit.Box(rect, "Bar", 0f, 0f, width, BarHeight), WindowKit.Bar, Chamfer, 0f, true);
            WindowKit.Shape(ScreenKit.Box(rect, "Frame", 0f, 0f, width, height), WindowKit.Ink, Chamfer, Stroke);
            WindowKit.Label(rect, "Title", title.ToUpperInvariant(), 20f, 0f, width - 40f, BarHeight, 14,
                WindowKit.Ink, font);
            WindowKit.Label(rect, "Corner", corner.ToUpperInvariant(), 20f, 0f, width - 40f, BarHeight, 14,
                WindowKit.Ink, font, TextAnchor.MiddleRight);
            window.Body = ScreenKit.Box(rect, "Body", 0f, BarHeight, width, height - BarHeight);
            window.fill = window.Body.gameObject.AddComponent<RectMask2D>();
            window.seam = ScreenKit.Box(rect, "Seam", 0f, 0f, 0f, 0f);
            window.seam.anchorMin = Vector2.zero;
            window.seam.anchorMax = Vector2.one;
            WindowKit.Shape(window.seam, WindowKit.Ink, 0f, Stroke);
            window.Place();
            return window;
        }

        /// <summary>Opens from closed over <see cref="OpenSeconds"/> of real time.</summary>
        public void Play()
        {
            playedAt = Time.unscaledTime;
            Open = 0f;
        }

        private void Update()
        {
            if (float.IsNegativeInfinity(playedAt)) return;
            Open = (Time.unscaledTime - playedAt) / OpenSeconds;
            if (open >= 1f) playedAt = float.NegativeInfinity;
        }

        private void Place()
        {
            var size = root.sizeDelta;
            var widen = EaseOut(open / WidenEnd);
            var unfolded = EaseOut((open - WidenEnd) / (UnfoldEnd - WidenEnd));
            var filled = Mathf.Floor(Mathf.Clamp01((open - UnfoldEnd) / (1f - UnfoldEnd)) * FillSteps) / FillSteps;
            // Closed, the clip has no area and nothing draws; from there a line as thick as the outline.
            var sides = open <= 0f ? size.x * .5f : (1f - widen) * size.x * .5f;
            var ends = open <= 0f ? size.y * .5f : (1f - unfolded) * (size.y * .5f - Stroke);
            unfold.padding = new Vector4(sides, ends, sides, ends);
            fill.padding = new Vector4(0f, (1f - filled) * Body.sizeDelta.y, 0f, 0f);
            seam.offsetMin = new Vector2(sides, ends);
            seam.offsetMax = new Vector2(-sides, -ends);
            seam.gameObject.SetActive(open > 0f && open < UnfoldEnd);
        }

        private static float EaseOut(float t)
        {
            var rest = 1f - Mathf.Clamp01(t);
            return 1f - rest * rest * rest;
        }
    }
}
