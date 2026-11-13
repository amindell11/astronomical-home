using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI.Screens
{
    /// <summary>
    /// The pause's uGUI: Resume and Quit run, clickable and keyed, answered through
    /// <see cref="Choice"/>. The host stops the clock and acts on the answer.
    /// </summary>
    public sealed class PauseScreen : MonoBehaviour
    {
        public enum Option { Resume, QuitRun }

        private readonly List<InputAction> actions = new();
        private Font font;

        /// <summary>Null until answered; the first answer stands.</summary>
        public Option? Choice { get; private set; }

        public static PauseScreen Create(Transform parent, Font font)
        {
            var go = new GameObject("PauseScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ScreenKit.Canvas(go);
            var screen = go.AddComponent<PauseScreen>();
            screen.font = font;
            screen.Build();
            screen.BindKeys();
            return screen;
        }

        public void Choose(Option option) => Choice ??= option;

        private void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            actions.Clear();
        }

        private void Build()
        {
            ScreenKit.Dim(transform);
            ScreenKit.Label(ScreenKit.Centered(transform, "Title", 1000f, 72f, 110f), "PAUSED", 40,
                ScreenKit.Paint, font, TextAnchor.MiddleCenter);
            BuildOption("Resume", "RESUME", "ESC", 20f, Option.Resume);
            BuildOption("QuitRun", "QUIT RUN", "Q", -30f, Option.QuitRun);
        }

        private void BuildOption(string name, string caption, string key, float y, Option option)
        {
            var box = ScreenKit.Centered(transform, name, 216f, 38f, y);
            ScreenKit.Fill(box, ScreenKit.Backing);
            ScreenKit.Button(box, () => Choose(option));
            ScreenKit.Frame(box);
            ScreenKit.Label(ScreenKit.Box(box, "Option", 14, 0, 140, 38), caption, 11, ScreenKit.Ink, font);
            ScreenKit.Label(ScreenKit.Box(box, "Key", 140, 0, 62, 38), key, 11, ScreenKit.Muted, font,
                TextAnchor.MiddleRight);
        }

        private void BindKeys()
        {
            Listen(nameof(Option.Resume), "<Keyboard>/escape", () => Choose(Option.Resume),
                "<Gamepad>/start", "<Gamepad>/buttonEast");
            Listen(nameof(Option.QuitRun), "<Keyboard>/q", () => Choose(Option.QuitRun),
                "<Gamepad>/buttonWest");
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
