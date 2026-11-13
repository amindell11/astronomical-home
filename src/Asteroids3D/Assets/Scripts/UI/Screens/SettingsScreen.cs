using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens
{
    /// <summary>
    /// The settings' uGUI: the rows it was given and Back, keyed. It owns only its controls; the host
    /// persists and applies each value through that row's callback.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        private readonly List<InputAction> actions = new();
        private SettingsRow[] rows = Array.Empty<SettingsRow>();
        private Slider[] rowSliders = Array.Empty<Slider>();
        private Text[] values = Array.Empty<Text>();
        private GameObject[] markers = Array.Empty<GameObject>();
        private Font font;

        public bool Closed { get; private set; }

        public int Selected { get; private set; }

        public static SettingsScreen Create(Transform parent, Font font, IReadOnlyList<SettingsRow> rows)
        {
            if (rows == null || rows.Count == 0)
                throw new ArgumentException("At least one row is required.", nameof(rows));

            var go = new GameObject("SettingsScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ScreenKit.Canvas(go);
            var screen = go.AddComponent<SettingsScreen>();
            screen.font = font;
            screen.Build(rows);
            screen.BindKeys();
            return screen;
        }

        public void Close() => Closed = true;

        public void Step(int direction)
        {
            var slider = rowSliders[Selected];
            if (slider)
            {
                slider.value = Mathf.Clamp01(slider.value + direction * 0.1f);
                return;
            }

            rows[Selected].Step(direction);
            values[Selected].text = rows[Selected].ValueText;
        }

        public void Select(int row)
        {
            Selected = Mathf.Clamp(row, 0, rows.Length - 1);
            for (var i = 0; i < markers.Length; i++)
                markers[i].SetActive(i == Selected);
        }

        private void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            actions.Clear();
        }

        private void Build(IReadOnlyList<SettingsRow> specs)
        {
            ScreenKit.Dim(transform);
            ScreenKit.Label(ScreenKit.Centered(transform, "Title", 1000f, 72f, 250f), "SETTINGS", 40,
                ScreenKit.Paint, font, TextAnchor.MiddleCenter);

            var count = specs.Count;
            rows = new SettingsRow[count];
            rowSliders = new Slider[count];
            values = new Text[count];
            markers = new GameObject[count];
            for (var i = 0; i < count; i++)
            {
                var spec = specs[i];
                rows[i] = spec;
                var row = ScreenKit.Centered(transform, "Row" + i, 420f, 38f, 180f - i * 44f);
                ScreenKit.Fill(row, ScreenKit.Backing);
                var marker = ScreenKit.Box(row, "Marker", 0, 0, 2, 38);
                ScreenKit.Fill(marker, ScreenKit.Paint);
                marker.gameObject.SetActive(i == 0);
                markers[i] = marker.gameObject;
                ScreenKit.Label(ScreenKit.Box(row, "Label", 14, 0, 110, 38), spec.Label, 11, ScreenKit.Ink, font);

                Text valueText;
                if (spec.IsSlider)
                {
                    valueText = ScreenKit.Label(ScreenKit.Box(row, "Value", 340, 0, 66, 38), spec.ValueText,
                        11, ScreenKit.Muted, font, TextAnchor.MiddleRight);
                    rowSliders[i] = BuildSlider(row, spec.SliderValue, value =>
                    {
                        spec.OnSlider(value);
                        valueText.text = spec.ValueText;
                    });
                }
                else
                {
                    var control = ScreenKit.Box(row, "Control", 130, 0, 276, 38);
                    valueText = ScreenKit.Label(ScreenKit.Box(row, "Value", 130, 0, 276, 38), spec.ValueText,
                        11, ScreenKit.Muted, font, TextAnchor.MiddleRight);
                    ScreenKit.Button(control, () =>
                    {
                        spec.Click();
                        valueText.text = spec.ValueText;
                    });
                }

                values[i] = valueText;
            }

            var lastRowY = 180f - (count - 1) * 44f;
            var back = ScreenKit.Centered(transform, "Back", 216f, 38f, lastRowY - 38f - 30f);
            ScreenKit.Fill(back, ScreenKit.Backing);
            ScreenKit.Button(back, Close);
            ScreenKit.Frame(back);
            ScreenKit.Label(ScreenKit.Box(back, "Option", 14, 0, 140, 38), "BACK", 11, ScreenKit.Ink, font);
            ScreenKit.Label(ScreenKit.Box(back, "Key", 140, 0, 62, 38), "ESC", 11, ScreenKit.Muted, font,
                TextAnchor.MiddleRight);
        }

        private static Slider BuildSlider(RectTransform row, float value, Action<float> onChanged)
        {
            var rect = ScreenKit.Box(row, "Slider", 130, 0, 200, 38);
            var background = ScreenKit.Box(rect, "Background", 0, 18, 200, 2);
            ScreenKit.Fill(background, ScreenKit.Line);

            var fillArea = ScreenKit.Box(rect, "Fill Area", 0, 18, 200, 2);
            var fill = ScreenKit.Box(fillArea, "Fill", 0, 0, 200, 2);
            ScreenKit.Fill(fill, ScreenKit.Paint);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            var handleArea = ScreenKit.Box(rect, "Handle Slide Area", 0, 10, 200, 18);
            var handle = ScreenKit.Box(handleArea, "Handle", 0, 0, 10, 0);
            handle.pivot = new Vector2(.5f, .5f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = ScreenKit.Ink;

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handleImage;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            // Slider skips a visual update when the value does not change.
            slider.SetValueWithoutNotify(value == 0f ? 1f : 0f);
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        private void BindKeys()
        {
            Listen("Back", "<Keyboard>/escape", Close, "<Gamepad>/buttonEast", "<Gamepad>/start");
            Listen("Up", "<Keyboard>/upArrow", () => Select(Selected - 1), "<Gamepad>/dpad/up");
            Listen("Down", "<Keyboard>/downArrow", () => Select(Selected + 1), "<Gamepad>/dpad/down");
            Listen("Left", "<Keyboard>/leftArrow", () => Step(-1), "<Gamepad>/dpad/left");
            Listen("Right", "<Keyboard>/rightArrow", () => Step(1), "<Gamepad>/dpad/right");
        }

        private void Listen(string name, string binding, Action onPerformed, params string[] more)
        {
            var action = new InputAction(name, InputActionType.Button, binding);
            foreach (var path in more) action.AddBinding(path);
            action.performed += _ => onPerformed();
            action.Enable();
            actions.Add(action);
        }
    }
}
