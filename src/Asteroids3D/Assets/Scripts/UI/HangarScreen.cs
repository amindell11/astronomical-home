using System;
using System.Collections.Generic;
using Ships;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Between-run hangar screen: renders the player's loadout slots as rows of selectable options and
    /// a Launch button. The prefab authors the static chrome (canvas, panel, row containers, an option
    /// button template, the Launch button); this component populates each row from the
    /// <see cref="LoadoutConfig"/> catalog at runtime and writes the picks into the pending
    /// <see cref="ShipLoadout"/>. Nothing is applied to the live ship here — the caller
    /// (<see cref="Player.PlayerRig"/>) installs the selection when Launch fires.
    ///
    /// Rows are built generically (<see cref="BuildRow{T}"/>), so adding the Ship slot is just another
    /// row over the same machinery.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HangarScreen : MonoBehaviour
    {
        [Header("Row containers (option buttons are cloned into these)")]
        [SerializeField] private Transform engineRow;
        [SerializeField] private Transform shieldRow;

        [Tooltip("Disabled button cloned once per option. Needs a child Text/label.")]
        [SerializeField] private Button optionButtonTemplate;

        [Header("Commit")]
        [SerializeField] private Button launchButton;

        [Header("Stats readout")]
        [Tooltip("Shows the hovered option's stats; falls back to the current selection.")]
        [SerializeField] private Text statsText;

        [Header("Selection tint")]
        [SerializeField] private Color selectedColor = new(0.20f, 0.55f, 0.95f, 1f);
        [SerializeField] private Color unselectedColor = new(0.20f, 0.20f, 0.24f, 1f);

        private readonly List<Action> refreshers = new();

        /// <summary>
        /// Populate the screen from <paramref name="catalog"/>, seed highlights from
        /// <paramref name="loadout"/>, and invoke <paramref name="onLaunch"/> when the player commits.
        /// The screen mutates <paramref name="loadout"/> in place as options are picked.
        /// </summary>
        public void Show(LoadoutConfig catalog, ShipLoadout loadout, Action onLaunch)
        {
            EnsureEventSystem();

            if (optionButtonTemplate)
                optionButtonTemplate.gameObject.SetActive(false);

            if (catalog)
            {
                BuildRow(engineRow, catalog.engines, () => loadout.Engine, m => loadout.Engine = m, Describe);
                BuildRow(shieldRow, catalog.shields, () => loadout.Shield, m => loadout.Shield = m, Describe);
            }

            RefreshHighlights();
            if (statsText) statsText.text = "";

            if (launchButton)
            {
                launchButton.onClick.RemoveAllListeners();
                launchButton.onClick.AddListener(() => onLaunch?.Invoke());
            }
        }

        // Clone the template once per option into the row; each clone selects its option and refreshes
        // the row's highlights on click, and shows its stats in the readout on hover. getCurrent/
        // setCurrent read and write the owning loadout field, so the same code drives any slot
        // (engine, shield, and later ship).
        private void BuildRow<T>(Transform row, IReadOnlyList<T> options, Func<T> getCurrent, Action<T> setCurrent,
            Func<T, string> describe)
            where T : UnityEngine.Object
        {
            if (!row || !optionButtonTemplate || options == null) return;

            foreach (var option in options)
            {
                if (!option) continue;
                var button = Instantiate(optionButtonTemplate, row);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<Text>();
                if (label) label.text = option.name;

                var captured = option;
                button.onClick.AddListener(() =>
                {
                    setCurrent(captured);
                    RefreshHighlights();
                });
                AddHoverStats(button.gameObject, () => describe(captured));
            }

            refreshers.Add(() => TintRow(row, options, getCurrent));
        }

        // Show the option's stats in the readout while the pointer is over it; clear on exit.
        private void AddHoverStats(GameObject button, Func<string> stats)
        {
            if (!statsText) return;
            var trigger = button.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => statsText.text = stats());
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => statsText.text = "");
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        private static string Describe(EngineModule engine) =>
            $"Top speed {engine.maxSpeed:0}   |   Turn {engine.maxYawRate:0} deg/s   |   Thrust {engine.forwardForce:0}   |   " +
            $"Strafe {engine.maxStrafeForce:0}   |   Boost {engine.boostImpulse:0} ({engine.boostCooldown:0.#}s cooldown)";

        private static string Describe(ShieldModule shield) =>
            $"Capacity {shield.maxShield:0}   |   Regen {shield.shieldRegenRate:0.#}/s after {shield.shieldRegenDelay:0.#}s";

        private void TintRow<T>(Transform row, IReadOnlyList<T> options, Func<T> getCurrent)
            where T : UnityEngine.Object
        {
            var current = getCurrent();
            var i = 0;
            foreach (Transform child in row)
            {
                if (child == optionButtonTemplate.transform) continue;
                if (i >= options.Count) break;
                var image = child.GetComponent<Image>();
                if (image) image.color = ReferenceEquals(options[i], current) ? selectedColor : unselectedColor;
                i++;
            }
        }

        private void RefreshHighlights()
        {
            foreach (var refresh in refreshers) refresh();
        }

        // uGUI needs an EventSystem to route pointer clicks; the game ships without one (HUD is
        // display-only), so create a legacy-input one on demand if the scene has none.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
