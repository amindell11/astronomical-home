using System;
using System.Text;
using Substrate.Services.Objectives;
using UI.Projection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    /// <summary>Lists the sector boosts in force under the phase display, one line each, in the find colour.</summary>
    public sealed class BoostReadout : MonoBehaviour
    {
        private static readonly Color FindColour = new(.72f, .48f, 1f, 1f);

        private readonly StringBuilder lines = new();
        private Text text;
        private IObjectiveService objectives;
        private int shown = -1;

        public static BoostReadout Create(Transform parent, Font font, Material material, CanopyProjection projection = null,
            Func<bool> isVisible = null)
        {
            var go = new GameObject("BoostReadout", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, -150f);
            text.rectTransform.sizeDelta = new Vector2(420f, 60f);
            text.font = font;
            text.material = material;
            text.fontSize = 9;
            text.color = FindColour;
            text.alignment = TextAnchor.UpperCenter;
            text.raycastTarget = false;
            text.enabled = false;
            var readout = go.AddComponent<BoostReadout>();
            readout.text = text;
            readout.enabled = false;
            InstrumentAppear.Attach(go, projection, () => text.enabled && (isVisible?.Invoke() ?? true));
            return readout;
        }

        public void Initialize(IObjectiveService source)
        {
            objectives = source ?? throw new ArgumentNullException(nameof(source));
            enabled = true;
        }

        private void LateUpdate()
        {
            var boosts = objectives.Boosts;
            text.enabled = boosts.Count > 0;
            if (boosts.Count == shown) return;
            shown = boosts.Count;
            lines.Clear();
            foreach (var boost in boosts)
                lines.AppendLine(boost.Label.ToUpperInvariant());
            text.text = lines.ToString().TrimEnd();
        }
    }
}
