using System;
using System.Collections.Generic;
using Ships;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Between-run hangar screen: populates the prefab-authored rows from the <see cref="LoadoutConfig"/>
    /// catalog and writes picks into the pending <see cref="ShipLoadout"/>. Nothing touches the live
    /// ship — the caller installs the selection when Launch fires.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class HangarScreen : MonoBehaviour
    {
        [Header("Row containers (option buttons are cloned into these)")]
        [SerializeField] private Transform shipRow;
        [SerializeField] private Transform engineRow;
        [SerializeField] private Transform shieldRow;

        [Tooltip("Disabled button cloned once per option. Needs a child Text/label.")]
        [SerializeField] private Button optionButtonTemplate;

        [Header("Commit")]
        [SerializeField] private Button launchButton;

        [Header("Stats readout")]
        [Tooltip("Shows the hovered option's stats; falls back to the current selection.")]
        [SerializeField] private Text statsText;

        [Header("Ship preview")]
        [Tooltip("Displays the preview stage's render texture. Null → no 3D preview.")]
        [SerializeField] private RawImage previewImage;

        [Tooltip("Continue the turntable mid-rotation when switching ships; off = each ship enters at the canonical pose (nose screen-right).")]
        [SerializeField] private bool continueSpinOnSwitch;

        [Header("Selection tint")]
        [SerializeField] private Color selectedColor = new(0.20f, 0.55f, 0.95f, 1f);
        [SerializeField] private Color unselectedColor = new(0.20f, 0.20f, 0.24f, 1f);

        private HangarPreviewStage previewStage;
        private readonly List<Action> refreshers = new();

        /// <summary>Mutates <paramref name="loadout"/> in place as options are picked.</summary>
        public void Show(LoadoutConfig catalog, ShipLoadout loadout, Action onLaunch)
        {
            EnsureEventSystem();

            if (optionButtonTemplate)
                optionButtonTemplate.gameObject.SetActive(false);

            if (previewImage)
            {
                previewStage = HangarPreviewStage.Create(continueSpinOnSwitch);
                previewImage.texture = previewStage.Texture;
                previewStage.Show(loadout);
            }

            if (catalog)
            {
                // Picking a ship reseeds the module slots to that ship's authored kit.
                BuildRow(shipRow, catalog.ships, () => loadout.Ship, s =>
                {
                    loadout.Ship = s;
                    loadout.Engine = s.Engine;
                    loadout.Shield = s.Shield;
                    if (previewStage) previewStage.Show(loadout);
                }, Describe);
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

        private static string Describe(Ship ship) =>
            $"Hull {ship.maxHealth:0}   |   Mass {ship.mass:0}   |   Bank {ship.maxBankAngle:0} deg   |   " +
            $"Kit: {(ship.Engine ? ship.Engine.name : "no engine")} + {(ship.Shield ? ship.Shield.name : "no shield")}";

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

        private void OnDestroy()
        {
            if (previewStage)
                Destroy(previewStage.gameObject);
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
