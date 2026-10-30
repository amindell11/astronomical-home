using System;
using Objectives;
using Objectives.Presentation;
using Substrate.Services.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    public sealed class ObjectiveStatusReadout : MonoBehaviour
    {
        private Text label;
        private Text clock;
        private Text caption;
        private Text announcement;
        private RectTransform display;
        private Font labelFont;
        private Image[] segments = Array.Empty<Image>();
        private IObjectiveService objectives;
        private ObjectiveTracker announcedTracker;
        private float openedAt;

        public static ObjectiveStatusReadout Create(Transform parent, Font labelFont, Font headingFont, Font timerFont)
        {
            var go = new GameObject("ObjectiveStatusReadout", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            go.layer = parent.gameObject.layer;
            var displayObject = new GameObject("Phase display", typeof(RectTransform));
            displayObject.layer = go.layer;
            displayObject.transform.SetParent(go.transform, false);
            var display = (RectTransform)displayObject.transform;
            display.anchorMin = display.anchorMax = new Vector2(.5f, 1f);
            display.pivot = new Vector2(.5f, 1f);
            display.anchoredPosition = new Vector2(0f, -40f);
            display.sizeDelta = new Vector2(420f, 100f);
            var label = CreateText(display, "Label", labelFont, 18, 0f, new Vector2(420f, 28f));
            var clock = CreateText(display, "Phase clock", timerFont, 23, 0f, new Vector2(240f, 32f));
            var caption = CreateText(display, "Phase caption", labelFont, 5, -41f, new Vector2(420f, 24f));
            var announcement = CreateText(go.transform, "Extraction announcement", headingFont, 13, 0f,
                new Vector2(420f, 28f));
            announcement.rectTransform.anchorMin = announcement.rectTransform.anchorMax = new Vector2(.5f, .5f);
            announcement.rectTransform.pivot = new Vector2(.5f, .5f);
            announcement.alignment = TextAnchor.MiddleCenter;
            announcement.text = "EXTRACTION OPEN";
            var readout = go.AddComponent<ObjectiveStatusReadout>();
            readout.label = label;
            readout.clock = clock;
            readout.caption = caption;
            readout.announcement = announcement;
            readout.display = display;
            readout.labelFont = labelFont;
            return readout;
        }

        private static Text CreateText(Transform parent, string name, Font font, int size, float y, Vector2 bounds)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.gameObject.layer = parent.gameObject.layer;
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 1f);
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, y);
            text.rectTransform.sizeDelta = bounds;
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperCenter;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, 1f);
            image.rectTransform.pivot = new Vector2(.5f, 1f);
            image.rectTransform.sizeDelta = size;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void Awake() => enabled = false;

        public void Initialize(IObjectiveService objectives)
        {
            this.objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
            announcedTracker = null;
            enabled = true;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            var tracker = objectives.SpineTracker;
            var progress = tracker?.PhaseProgress;
            var extraction = objectives.SpineState == ObjectiveType.ExtractionChallenge && objectives.SpineTarget;
            if (!extraction) announcedTracker = null;
            else if (tracker != announcedTracker)
            {
                announcedTracker = tracker;
                openedAt = Time.unscaledTime;
            }

            var elapsed = Time.unscaledTime - openedAt;
            var opening = extraction && elapsed < 2.05f;
            var openingAlpha = Mathf.Clamp01(Mathf.Min(elapsed / .22f, (2.05f - elapsed) / .45f));
            announcement.enabled = opening;
            announcement.color = new Color(1f, 1f, 1f, openingAlpha);
            if (progress.HasValue && tracker.CurrentState is ObjectiveType.Survive or ObjectiveType.ExtractionChallenge)
            {
                ApplyPhase(progress.Value, tracker.CurrentStatusLine);
                return;
            }

            clock.enabled = caption.enabled = false;
            display.anchoredPosition = new Vector2(0f, -40f);
            foreach (var segment in segments) segment.enabled = false;
            label.enabled = true;
            label.fontSize = 18;
            label.font = labelFont;
            label.alignment = TextAnchor.UpperCenter;
            label.rectTransform.sizeDelta = new Vector2(420f, 28f);
            label.text = tracker?.CurrentStatusLine ?? string.Empty;
        }

        private void ApplyPhase(SectorPhaseProgress phase, string instruction)
        {
            display.anchoredPosition = new Vector2(0f, -20f);
            clock.enabled = !phase.ExtractionOpen;
            label.enabled = phase.ExtractionOpen;
            caption.enabled = true;
            label.font = labelFont;
            label.fontSize = 10;
            label.alignment = TextAnchor.UpperCenter;
            label.rectTransform.sizeDelta = new Vector2(420f, 28f);
            label.text = "EXTRACTION OPEN";
            var seconds = Mathf.CeilToInt(phase.SecondsLeft);
            clock.text = $"{seconds / 60}:{seconds % 60:00}";
            caption.text = phase.ExtractionOpen ? instruction : phase.Index == phase.Count - 1 ? "EXTRACTION" : "NEXT PHASE";
            caption.fontSize = phase.ExtractionOpen ? 10 : 5;
            caption.rectTransform.anchoredPosition = new Vector2(0f, phase.ExtractionOpen ? -36f : -41f);

            if (segments.Length != phase.Count)
            {
                foreach (var segment in segments) Destroy(segment.gameObject);
                segments = new Image[phase.Count];
                for (var i = 0; i < segments.Length; i++)
                    segments[i] = CreateImage(display, $"Sector phase {i + 1}", Vector2.zero, Color.white);
            }

            var x = -(24f + (phase.Count - 1) * 19.5f) / 2f;
            for (var i = 0; i < segments.Length; i++)
            {
                var width = i == phase.Index ? 24f : 13.5f;
                var segment = segments[i];
                segment.enabled = !phase.ExtractionOpen;
                segment.rectTransform.sizeDelta = new Vector2(width, 1.5f);
                segment.rectTransform.anchoredPosition = new Vector2(x + width / 2f, -33f);
                segment.color = new Color(1f, 1f, 1f, i > phase.Index ? .32f : 1f);
                x += width + 6f;
            }
        }
    }
}
