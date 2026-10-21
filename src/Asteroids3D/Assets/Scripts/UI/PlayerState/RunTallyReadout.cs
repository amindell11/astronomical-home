using Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    /// <summary>
    /// Top-center kills and time survived line. Code-built under the overlay's root canvas so the
    /// HUD prefab stays untouched; polls the bound <see cref="IRunTally"/>.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class RunTallyReadout : MonoBehaviour
    {
        private Text label;
        private IRunTally tally;
        private int shownKills = -1;
        private int shownSeconds = -1;

        public static RunTallyReadout Create(Transform parent)
        {
            var go = new GameObject("RunTallyReadout", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(320f, 28f);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperCenter;
            text.raycastTarget = false;
            return go.AddComponent<RunTallyReadout>();
        }

        private void Awake()
        {
            label = GetComponent<Text>();
        }

        /// <summary>Re-bindable: the persistent HUD re-Initializes when the player is rebuilt.</summary>
        public void Initialize(IRunTally tally)
        {
            this.tally = tally;
            Apply();
        }

        private void Update()
        {
            if (tally == null) return;
            Apply();
        }

        private void Apply()
        {
            if (tally == null)
            {
                label.text = string.Empty;
                shownKills = shownSeconds = -1;
                return;
            }

            // The line changes at most once a second; skip the per-frame string rebuild otherwise.
            var seconds = Mathf.FloorToInt(tally.SecondsSurvived);
            if (tally.Kills == shownKills && seconds == shownSeconds) return;
            shownKills = tally.Kills;
            shownSeconds = seconds;
            label.text = $"KILLS {shownKills}    TIME {RunTally.FormatSeconds(seconds)}";
        }
    }
}
