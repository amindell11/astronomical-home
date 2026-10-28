using System;
using System.Collections.Generic;
using Combat.Weapons.Mods;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens
{
    /// <summary>
    /// The lull pick's uGUI: up to three weapon mod cards keyed 1, 2, 3 and a skip, built at rest and
    /// answered through <see cref="Choice"/>. It owns only its keys; the host stops the clock, silences
    /// the player and applies the pick. How it was ruled to read: https://github.com/amindell11/astronomical-home/issues/1211#issuecomment-6464116762
    /// </summary>
    public sealed class LullPickScreen : MonoBehaviour
    {
        public const int MaxOffers = 3;
        public const int Skip = -1;

        private static readonly Color Ink = new(.93f, .94f, .93f);
        private static readonly Color Muted = new(.70f, .74f, .81f);
        private static readonly Color Line = new(.84f, .87f, .93f, .25f);
        private static readonly Color Paint = new(.91f, .91f, .85f);
        private static readonly Color Gain = new(.56f, .83f, .67f);
        private static readonly Color Backing = new(.047f, .078f, .153f, .9f);
        private static readonly Color Dim = new(.047f, .078f, .153f, .55f);
        private static readonly string[] Keys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3" };
        private static readonly string[] PadButtons =
            { "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth", "<Gamepad>/buttonEast" };
        private const float CardWidth = 300f;
        private const float CardHeight = 190f;
        private const float CardGap = 24f;

        private readonly List<InputAction> actions = new();
        private Font font;
        private int offerCount;

        /// <summary>Null until answered; then the picked offer's index, or <see cref="Skip"/>.</summary>
        public int? Choice { get; private set; }

        public static LullPickScreen Create(Transform parent, IReadOnlyList<WeaponMod> offers,
            Func<WeaponMod, string> landing)
        {
            if (offers == null || offers.Count == 0 || offers.Count > MaxOffers)
                throw new ArgumentException($"A lull pick offers one to {MaxOffers} mods.", nameof(offers));
            var go = new GameObject("LullPickScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
            var screen = go.AddComponent<LullPickScreen>();
            screen.offerCount = offers.Count;
            screen.Build(offers, landing);
            screen.BindKeys();
            return screen;
        }

        /// <summary>Answers the screen with an offer's index or <see cref="Skip"/>; the first answer stands.</summary>
        public void Choose(int choice)
        {
            if (choice != Skip && (choice < 0 || choice >= offerCount))
                throw new ArgumentOutOfRangeException(nameof(choice), choice, "Not an offered card.");
            if (Choice.HasValue) return;
            Choice = choice;
        }

        private void Awake() => font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            actions.Clear();
        }

        private void Build(IReadOnlyList<WeaponMod> offers, Func<WeaponMod, string> landing)
        {
            var dim = Centered(transform, "Dim", 0f, 0f, 0f);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            // Translucent so the HUD's phase line still reads; a raycast target so clicks stop here.
            dim.gameObject.AddComponent<Image>().color = Dim;

            Label(Centered(transform, "Title", 600f, 48f, 160f), "MOD PICK", 40, Paint, TextAnchor.MiddleCenter);
            Label(Centered(transform, "Hint", 600f, 20f, 122f), "ONE KEY PICKS   ·   SPACE SKIPS", 12, Muted,
                TextAnchor.MiddleCenter);

            var count = offers.Count;
            var cards = Centered(transform, "Cards", count * CardWidth + (count - 1) * CardGap, CardHeight, 0f);
            for (var i = 0; i < count; i++)
                BuildCard(cards, i, offers[i], landing(offers[i]));
        }

        private void BuildCard(RectTransform cards, int index, WeaponMod mod, string landing)
        {
            var card = Box(cards, $"Card{index + 1}", index * (CardWidth + CardGap), 0f, CardWidth, CardHeight);
            Fill(card, Backing);
            Fill(Box(card, "CornerTop", 0, 0, 17, 1), Line);
            Fill(Box(card, "CornerLeft", 0, 0, 1, 17), Line);

            var cap = Box(card, "KeyCap", 18, 18, 40, 40);
            Fill(Box(cap, "Top", 0, 0, 40, 1), Line);
            Fill(Box(cap, "Bottom", 0, 39, 40, 1), Line);
            Fill(Box(cap, "Left", 0, 0, 1, 40), Line);
            Fill(Box(cap, "Right", 39, 0, 1, 40), Line);
            Label(Box(cap, "Key", 0, 0, 40, 40), (index + 1).ToString(), 22, Paint, TextAnchor.MiddleCenter);

            Label(Box(card, "Name", 72, 18, 210, 40), mod.DisplayName.ToUpperInvariant(), 18, Ink);
            Label(Box(card, "Effect", 18, 84, 264, 30), mod.Effect.ToUpperInvariant(), 18, Gain);
            Label(Box(card, "Landing", 18, 146, 264, 24), $"ON  {landing.ToUpperInvariant()}", 11, Muted);
        }

        private void BindKeys()
        {
            for (var i = 0; i < offerCount; i++)
            {
                var action = new InputAction($"Pick{i + 1}", InputActionType.Button, Keys[i]);
                action.AddBinding(PadButtons[i]);
                var index = i;
                action.performed += _ => Choose(index);
                action.Enable();
                actions.Add(action);
            }

            var skip = new InputAction("Skip", InputActionType.Button, "<Keyboard>/space");
            skip.AddBinding("<Gamepad>/buttonSouth");
            skip.performed += _ => Choose(Skip);
            skip.Enable();
            actions.Add(skip);
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
