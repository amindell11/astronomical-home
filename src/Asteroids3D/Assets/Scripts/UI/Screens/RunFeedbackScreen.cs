using System.Collections.Generic;
using UI.Screens.Windows;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens
{
    /// <summary>
    /// A playtest build's note box after a death: one window with a text field, saved with Enter or
    /// skipped with Escape, answered through <see cref="Note"/>. The host writes the note.
    /// </summary>
    public sealed class RunFeedbackScreen : MonoBehaviour
    {
        public const int MaxLength = 500;

        private const float Width = 640f;
        private const float Height = 286f;
        private const float Inset = 20f;

        private readonly List<InputAction> actions = new();
        private ScreenFonts fonts;
        private ScreenLayers layers;
        private ScreenWindow window;
        private InputField field;
        private bool focused;

        /// <summary>Null until answered; empty when skipped or saved blank. The first answer stands.</summary>
        public string Note { get; private set; }

        public static RunFeedbackScreen Create(Transform parent, ScreenCameras cameras)
        {
            var go = new GameObject("RunFeedbackScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layers = ScreenLayers.Create(go, cameras);
            var screen = go.AddComponent<RunFeedbackScreen>();
            screen.layers = layers;
            screen.Build();
            screen.BindKeys();
            return screen;
        }

        public void Save() => Note ??= field.text.Trim();

        public void Skip() => Note ??= string.Empty;

        private void Awake() => fonts = ScreenFonts.Load();

        // Focus waits for the window to open, so the key that closed the recap is up before typing starts.
        private void Update()
        {
            if (focused || window.Open < 1f) return;
            focused = true;
            field.ActivateInputField();
        }

        private void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            actions.Clear();
        }

        private void Build()
        {
            WindowKit.Fade(layers.Under, WindowKit.StrongFade);
            var panel = ScreenKit.Centered(transform, "Panel", Width, Height + 58f, 0f);
            window = ScreenWindow.Create(panel, layers, "Feedback", 0f, 0f, Width, Height, "PLAYTEST FEEDBACK",
                fonts.Label);
            var body = window.Body;
            var width = Width - 2f * Inset;
            WindowKit.Label(body, "Prompt", "WHAT KILLED YOU? WHAT FELT OFF?", Inset, 14f, width, 22f, 16,
                WindowKit.Ink, fonts.Label);
            BuildField(body, Inset, 46f, width, Height - ScreenWindow.BarHeight - 46f - Inset);
            WindowKit.KeyButton(layers, ScreenKit.Box(panel, "Skip", 0f, Height + 17f, 200f, 41f), "SKIP", "ESC",
                fonts.Label, Skip);
            WindowKit.KeyButton(layers, ScreenKit.Box(panel, "Save", Width - 260f, Height + 17f, 260f, 41f),
                "SAVE NOTE", "ENTER ›", fonts.Label, Save);
            window.Play();
        }

        private void BuildField(RectTransform body, float x, float y, float width, float height)
        {
            var box = ScreenKit.Box(body, "Field", x, y, width, height);
            var back = box.gameObject.AddComponent<Image>();
            back.color = WindowKit.Lit(layers, ScreenKit.Hover);
            WindowKit.Shape(ScreenKit.Box(box, "Frame", 0f, 0f, width, height), WindowKit.Line, 0f, 1.5f);
            var placeholder = Area(box, "Placeholder", width, height, WindowKit.Dim);
            placeholder.text = "Type a note, or press Esc to skip";
            var text = Area(box, "Text", width, height, WindowKit.Ink);
            field = box.gameObject.AddComponent<InputField>();
            field.targetGraphic = back;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = MaxLength;
            field.lineType = InputField.LineType.MultiLineSubmit;
            field.caretColor = WindowKit.Ink;
            field.customCaretColor = true;
            field.selectionColor = WindowKit.Lit(layers, new Color(1f, 1f, 1f, .3f));
            field.transition = Selectable.Transition.None;
        }

        private Text Area(RectTransform box, string name, float width, float height, Color color)
        {
            const float pad = 12f;
            var text = ScreenKit.Label(ScreenKit.Box(box, name, pad, pad, width - 2f * pad, height - 2f * pad), "",
                17, color, fonts.Label, TextAnchor.UpperLeft);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void BindKeys()
        {
            Listen("Save", Save, "<Keyboard>/enter", "<Keyboard>/numpadEnter");
            Listen("Skip", Skip, "<Keyboard>/escape", "<Gamepad>/buttonEast");
        }

        private void Listen(string name, System.Action onPerformed, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button, bindings[0]);
            for (var i = 1; i < bindings.Length; i++) action.AddBinding(bindings[i]);
            action.performed += _ => onPerformed();
            action.Enable();
            actions.Add(action);
        }
    }
}
