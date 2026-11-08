using System;
using System.Text;
using Substrate.Services.Objectives;
using UI.Objectives;
using UI.Projection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    /// <summary>Lists the timed boosts in force under the phase display, each with its whole seconds left, in the find colour.</summary>
    public sealed class BoostReadout : MonoBehaviour
    {
        private readonly StringBuilder lines = new();
        private Text text;
        private IObjectiveService objectives;

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
            text.color = FindPointers.FindColour;
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
            if (!text.enabled) return;
            lines.Clear();
            foreach (var boost in boosts)
                lines.Append(boost.Label.ToUpperInvariant()).Append("  ")
                    .Append(Mathf.CeilToInt(Mathf.Max(0f, boost.EndsAt - Time.time))).AppendLine("s");
            text.text = lines.ToString().TrimEnd();
        }
    }
}
