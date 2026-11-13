using System;
using Objectives;
using Objectives.Presentation;
using Substrate.Services.Objectives;
using UnityEngine;
using UnityEngine.UI;
using UI.Projection;
using UI.Prompts;

namespace UI.PlayerState
{
    public sealed class ObjectiveStatusReadout : MonoBehaviour
    {
        private const float BannerSeconds = 2.05f;
        private const float BannerFadeIn = .22f;
        private const float BannerFadeOut = .45f;
        private const float SpotBarWidth = 160f;
        private static readonly Color SpottedRed = new(1f, .25f, .2f);

        private enum Banner { None, Opening, Spotted, Coming }

        private Text label;
        private Text clock;
        private int clockSeconds = -1;
        private Text caption;
        private Text announcement;
        private KeyPrompt keyPrompt;
        private RectTransform display;
        private Font labelFont;
        private Image[] segments = Array.Empty<Image>();
        private IObjectiveService objectives;
        private ObjectiveTracker announcedTracker;
        private ObjectiveType announcedState;
        private Transform announcedTarget;
        private Image spotBar;
        private Banner banner;
        private float bannerAt;
        private ObjectiveTracker huntTracker;
        private HuntReading lastHunt;

        public static ObjectiveStatusReadout Create(Transform parent, Font labelFont, Font headingFont, Font timerFont,
            Font keyFont, Material textMaterial, CanopyProjection projection = null, Func<bool> isVisible = null)
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
            var label = CreateText(display, "Label", labelFont, textMaterial, 18, 0f, new Vector2(420f, 28f));
            var clock = CreateText(display, "Phase clock", timerFont, textMaterial, 23, 0f, new Vector2(240f, 32f));
            var caption = CreateText(display, "Phase caption", labelFont, textMaterial, 5, -41f, new Vector2(420f, 24f));
            var keyPrompt = KeyPrompt.Create(display, keyFont, labelFont, textMaterial, string.Empty, string.Empty);
            var keyPromptRect = (RectTransform)keyPrompt.transform;
            keyPromptRect.anchorMin = keyPromptRect.anchorMax = keyPromptRect.pivot = new Vector2(.5f, 1f);
            keyPrompt.gameObject.SetActive(false);
            var announcement = CreateText(go.transform, "Extraction announcement", headingFont, textMaterial, 13, 0f,
                new Vector2(420f, 28f));
            announcement.rectTransform.anchorMin = announcement.rectTransform.anchorMax = new Vector2(.5f, .5f);
            announcement.rectTransform.pivot = new Vector2(.5f, .5f);
            announcement.alignment = TextAnchor.MiddleCenter;
            var spotBar = CreateImage(announcement.transform, "Spotted bar", new Vector2(SpotBarWidth, 2f), SpottedRed);
            spotBar.rectTransform.anchorMin = spotBar.rectTransform.anchorMax = new Vector2(.5f, 0f);
            spotBar.rectTransform.pivot = new Vector2(.5f, 1f);
            spotBar.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            spotBar.enabled = false;
            InstrumentAppear.Attach(announcement.gameObject, projection,
                () => announcement.enabled && announcement.color.a > 0f && (isVisible?.Invoke() ?? true));
            var readout = go.AddComponent<ObjectiveStatusReadout>();
            readout.label = label;
            readout.clock = clock;
            readout.caption = caption;
            readout.announcement = announcement;
            readout.spotBar = spotBar;
            readout.keyPrompt = keyPrompt;
            readout.display = display;
            readout.labelFont = labelFont;
            return readout;
        }

        private static Text CreateText(Transform parent, string name, Font font, Material material, int size, float y, Vector2 bounds)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.gameObject.layer = parent.gameObject.layer;
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 1f);
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, y);
            text.rectTransform.sizeDelta = bounds;
            text.font = font;
            text.material = material;
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
            huntTracker = null;
            banner = Banner.None;
            enabled = true;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            var tracker = objectives.SpineTracker;
            var progress = tracker?.PhaseProgress;
            var state = objectives.SpineState;
            var target = objectives.SpineTarget;
            // A survive-state target is a lull beacon; extraction retargets without replaying its opening.
            var open = target && (state is ObjectiveType.Survive or ObjectiveType.ExtractionChallenge);
            if (!open)
            {
                announcedTracker = null;
                if (banner == Banner.Opening) banner = Banner.None;
            }
            else if (tracker != announcedTracker || state != announcedState
                     || state == ObjectiveType.Survive && target != announcedTarget)
            {
                announcedTracker = tracker;
                announcedState = state.Value;
                announcedTarget = target;
                Show(Banner.Opening, state == ObjectiveType.Survive ? "MOD BEACON" : "EXTRACTION OPEN");
            }

            ReadHunt(tracker, progress);
            var hunt = progress?.Hunt ?? HuntReading.None;
            var elapsed = Time.unscaledTime - bannerAt;
            var spotted = banner == Banner.Spotted;
            var held = spotted || banner == Banner.Coming && hunt == HuntReading.Coming;
            announcement.enabled = held || banner != Banner.None && elapsed < BannerSeconds;
            var tint = spotted ? SpottedRed : Color.white;
            tint.a = Mathf.Clamp01(held
                ? elapsed / BannerFadeIn
                : Mathf.Min(elapsed / BannerFadeIn, (BannerSeconds - elapsed) / BannerFadeOut));
            announcement.color = tint;
            var showBar = spotted && progress.HasValue;
            spotBar.enabled = showBar;
            if (showBar)
                spotBar.rectTransform.sizeDelta = new Vector2(
                    SpotBarWidth * Mathf.Clamp01(progress.Value.SpottedFill), 2f);
            var prompt = tracker?.CurrentPrompt;
            if (progress.HasValue && tracker.CurrentState is ObjectiveType.Survive or ObjectiveType.ExtractionChallenge)
            {
                ApplyPhase(progress.Value, tracker.CurrentStatusLine, prompt);
                return;
            }

            clock.enabled = caption.enabled = false;
            display.anchoredPosition = new Vector2(0f, -40f);
            foreach (var segment in segments) segment.enabled = false;
            label.fontSize = 18;
            label.font = labelFont;
            label.alignment = TextAnchor.UpperCenter;
            label.rectTransform.sizeDelta = new Vector2(420f, 28f);
            label.text = tracker?.CurrentStatusLine ?? string.Empty;
            ShowPrompt(prompt, label);
        }

        private void Show(Banner shown, string text)
        {
            banner = shown;
            bannerAt = Time.unscaledTime;
            announcement.text = text;
        }

        // Fades when that tracker's gap ends, not when the tracker changes.
        private void ReadHunt(ObjectiveTracker tracker, SectorPhaseProgress? progress)
        {
            var hunt = progress?.Hunt ?? HuntReading.None;
            var previous = tracker == huntTracker ? lastHunt : HuntReading.None;
            huntTracker = tracker;
            lastHunt = hunt;
            if (banner == Banner.Spotted && hunt != HuntReading.Spotted) banner = Banner.None;
            // Already shown, so the fade-out clock starts after its fade-in.
            if (banner == Banner.Coming && hunt != HuntReading.Coming && previous == HuntReading.Coming)
                bannerAt = Time.unscaledTime - BannerFadeIn;
            if (hunt == previous) return;
            if (hunt == HuntReading.Spotted) Show(Banner.Spotted, "SPOTTED");
            else if (hunt == HuntReading.Coming) Show(Banner.Coming, "THEY'RE COMING");
        }

        private static string Clock(float secondsLeft)
        {
            var seconds = Mathf.CeilToInt(secondsLeft);
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        private void ShowPrompt(InteractionPrompt? prompt, Text replaced)
        {
            replaced.enabled = !prompt.HasValue;
            keyPrompt.gameObject.SetActive(prompt.HasValue);
            if (!prompt.HasValue) return;
            keyPrompt.Set(prompt.Value.Key, prompt.Value.Action);
            ((RectTransform)keyPrompt.transform).anchoredPosition = replaced.rectTransform.anchoredPosition;
        }

        private void ApplyPhase(SectorPhaseProgress phase, string instruction, InteractionPrompt? prompt)
        {
            display.anchoredPosition = new Vector2(0f, -20f);
            clock.enabled = !phase.ExtractionOpen;
            label.enabled = phase.ExtractionOpen;
            label.font = labelFont;
            label.fontSize = 10;
            label.alignment = TextAnchor.UpperCenter;
            label.rectTransform.sizeDelta = new Vector2(420f, 28f);
            label.text = "EXTRACTION OPEN";
            var seconds = Mathf.CeilToInt(phase.SecondsLeft);
            if (seconds != clockSeconds)
            {
                clockSeconds = seconds;
                clock.text = Clock(phase.SecondsLeft);
            }
            caption.text = phase.ExtractionOpen ? instruction : phase.Hunt switch
            {
                HuntReading.Searching => "SEARCHING",
                HuntReading.Spotted => "SPOTTED",
                HuntReading.Coming => "INCOMING",
                _ => phase.Index == phase.Count - 1 ? "EXTRACTION" : "NEXT PHASE"
            };
            caption.fontSize = phase.ExtractionOpen ? 10 : 5;
            caption.rectTransform.anchoredPosition = new Vector2(0f, phase.ExtractionOpen ? -36f : -41f);
            ShowPrompt(phase.ExtractionOpen ? prompt : null, caption);

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
