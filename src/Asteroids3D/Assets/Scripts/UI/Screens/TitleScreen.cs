using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens
{
    /// <summary>
    /// The title's uGUI: the game's name with Play and Quit, clickable and keyed, answered through
    /// <see cref="Choice"/>. The host shows it over the hangar sky and acts on the answer.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        public const string GameName = "Astronomical: Survival";

        public enum Option { Play, Quit }

        private static readonly Color Ink = new(.93f, .94f, .93f);
        private static readonly Color Muted = new(.70f, .74f, .81f);
        private static readonly Color Line = new(.84f, .87f, .93f, .25f);
        private static readonly Color Paint = new(.91f, .91f, .85f);
        private static readonly Color Backing = new(.047f, .078f, .153f, .9f);
        private static readonly Color Hover = new(1f, 1f, 1f, .08f);

        private readonly List<InputAction> actions = new();
        private Font font;

        /// <summary>Null until answered; the first answer stands.</summary>
        public Option? Choice { get; private set; }

        public static TitleScreen Create(Transform parent)
        {
            var go = new GameObject("TitleScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
            var screen = go.AddComponent<TitleScreen>();
            screen.Build();
            screen.BindKeys();
            return screen;
        }

        public void Choose(Option option) => Choice ??= option;

        private void Awake() => font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            actions.Clear();
        }

        private void Build()
        {
            Label(Centered(transform, "Title", 1000f, 72f, 120f), GameName.ToUpperInvariant(), 56, Paint,
                TextAnchor.MiddleCenter);
            BuildOption("Play", "PLAY", "ENTER", -20f, Option.Play);
            BuildOption("Quit", "QUIT", "ESC", -70f, Option.Quit);
        }

        private void BuildOption(string name, string caption, string key, float y, Option option)
        {
            var box = Centered(transform, name, 216f, 38f, y);
            // Backing under the hover, so the button reads over the sky.
            Fill(box, Backing);
            Clickable(box, option);
            Fill(Box(box, "Top", 0, 0, 216, 1), Line);
            Fill(Box(box, "Bottom", 0, 37, 216, 1), Line);
            Fill(Box(box, "Left", 0, 0, 1, 38), Line);
            Fill(Box(box, "Right", 215, 0, 1, 38), Line);
            Label(Box(box, "Option", 14, 0, 140, 38), caption, 11, Ink);
            Label(Box(box, "Key", 140, 0, 62, 38), key, 11, Muted, TextAnchor.MiddleRight);
        }

        private void Clickable(RectTransform rect, Option option)
        {
            var overlay = Box(rect, "Hover", 0, 0, rect.sizeDelta.x, rect.sizeDelta.y);
            var image = overlay.gameObject.AddComponent<Image>();
            image.color = Hover;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.clear;
            colors.selectedColor = Color.clear;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.fadeDuration = 0f;
            button.colors = colors;
            button.onClick.AddListener(() => Choose(option));
        }

        private void BindKeys()
        {
            var play = new InputAction(nameof(Option.Play), InputActionType.Button, "<Keyboard>/enter");
            play.AddBinding("<Keyboard>/space");
            play.AddBinding("<Gamepad>/buttonSouth");
            play.AddBinding("<Gamepad>/start");
            play.performed += _ => Choose(Option.Play);
            play.Enable();
            actions.Add(play);

            var quit = new InputAction(nameof(Option.Quit), InputActionType.Button, "<Keyboard>/escape");
            quit.AddBinding("<Gamepad>/buttonEast");
            quit.performed += _ => Choose(Option.Quit);
            quit.Enable();
            actions.Add(quit);
        }

        private static RectTransform Centered(Transform parent, string name, float width, float height, float y)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private Text Label(RectTransform rect, string value, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }
    }
}
