using System;
using Objectives;
using Substrate.Services.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ObjectiveStatusReadout : MonoBehaviour
    {
        private Text label;
        private CanvasGroup group;
        private Image plate;
        private Image underline;
        private IObjectiveService objectives;
        private ObjectiveTracker announcedTracker;
        private float openedAt;

        public static ObjectiveStatusReadout Create(Transform parent, Material textMaterial)
        {
            var go = new GameObject("ObjectiveStatusReadout", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -40f);
            rect.sizeDelta = new Vector2(420f, 28f);

            go.layer = parent.gameObject.layer;
            var plate = CreateImage(go.transform, "Opening plate", new Vector2(125f, 20f),
                new Color(.025f, .08f, .095f, .94f));
            var underline = CreateImage(go.transform, "Opening underline", new Vector2(40f, .94f),
                new Color(.45f, .94f, .76f, 1f));
            underline.rectTransform.anchoredPosition = new Vector2(0, -20f);
            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.layer = go.layer;
            labelObject.transform.SetParent(go.transform, false);
            var text = labelObject.AddComponent<Text>();
            text.material = textMaterial;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 1f);
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperCenter;
            text.raycastTarget = false;
            var readout = go.AddComponent<ObjectiveStatusReadout>();
            readout.plate = plate;
            readout.underline = underline;
            return readout;
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

        private void Awake()
        {
            label = GetComponentInChildren<Text>();
            group = GetComponent<CanvasGroup>();
            enabled = false;
        }

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
            var extraction = objectives.SpineState == ObjectiveType.ExtractionChallenge && objectives.SpineTarget;
            if (!extraction) announcedTracker = null;
            else if (tracker != announcedTracker)
            {
                announcedTracker = tracker;
                openedAt = Time.unscaledTime;
            }

            var elapsed = Time.unscaledTime - openedAt;
            var opening = extraction && elapsed < 2.05f;
            plate.enabled = underline.enabled = opening;
            group.alpha = opening ? Mathf.Clamp01(Mathf.Min(elapsed / .22f, (2.05f - elapsed) / .45f)) : 1f;
            label.fontSize = opening ? 10 : 18;
            label.alignment = opening ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter;
            label.rectTransform.sizeDelta = opening ? new Vector2(125f, 20f) : new Vector2(420f, 28f);
            label.text = opening ? "Extraction open" : tracker?.CurrentStatusLine ?? string.Empty;
        }
    }
}
