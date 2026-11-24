using System;
using System.Collections.Generic;
using Loot;
using UI.Screens.Windows;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI.Screens
{
    /// <summary>
    /// The lull pick's uGUI: up to three lull mod windows (weapon or ship) and a skip, each clickable and
    /// keyed (1, 2, 3, Escape), opened on the unscaled clock and answered through <see cref="Choice"/>.
    /// It owns only its keys and buttons; the host stops the clock, silences the player and applies the pick.
    /// How it was ruled to read: https://github.com/amindell11/astronomical-home/issues/1694
    /// </summary>
    public sealed class LullPickScreen : MonoBehaviour
    {
        public const int MaxOffers = 3;
        public const int Skip = -1;

        private static readonly string[] Keys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3" };
        private static readonly string[] PadButtons =
            { "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth", "<Gamepad>/buttonEast" };
        private const float CardWidth = 333f;
        private const float CardHeight = 193f;
        private const float CardGap = 29f;
        private const float Inset = 16f;

        private readonly List<InputAction> actions = new();
        private ScreenFonts fonts;
        private ScreenLayers layers;
        private int offerCount;

        /// <summary>Null until answered; then the picked offer's index, or <see cref="Skip"/>.</summary>
        public int? Choice { get; private set; }

        public static LullPickScreen Create(Transform parent, IReadOnlyList<LullMod> offers,
            Func<LullMod, string> landing, ScreenCameras cameras)
        {
            if (offers == null || offers.Count == 0 || offers.Count > MaxOffers)
                throw new ArgumentException($"A lull pick offers one to {MaxOffers} mods.", nameof(offers));
            var go = new GameObject("LullPickScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layers = ScreenLayers.Create(go, cameras);
            var screen = go.AddComponent<LullPickScreen>();
            screen.layers = layers;
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

        private void Awake() => fonts = ScreenFonts.Load();

        private void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            actions.Clear();
        }

        private void Build(IReadOnlyList<LullMod> offers, Func<LullMod, string> landing)
        {
            WindowKit.Fade(layers.Under, WindowKit.StrongFade);
            ScreenKit.Label(ScreenKit.Centered(transform, "Title", 800f, 44f, 207f), "MOD PICK", 32, WindowKit.Ink,
                fonts.Title, TextAnchor.MiddleCenter);
            ScreenKit.Label(ScreenKit.Centered(transform, "Hint", 800f, 20f, 169f),
                "SELECT ONE PERMANENT SHIP OR WEAPON UPGRADE", 15, WindowKit.Dim, fonts.Label,
                TextAnchor.MiddleCenter);

            var count = offers.Count;
            var cards = ScreenKit.Centered(transform, "Cards", count * CardWidth + (count - 1) * CardGap, CardHeight,
                0f);
            for (var i = 0; i < count; i++)
                BuildCard(cards, i, offers[i], landing(offers[i]));

            WindowKit.KeyButton(layers, ScreenKit.Centered(transform, "Skip", 200f, 36f, -CardHeight / 2f - 41f),
                "SKIP", "ESC", fonts.Label, () => Choose(Skip));
        }

        private void BuildCard(RectTransform cards, int index, LullMod mod, string landing)
        {
            var window = ScreenWindow.Create(cards, layers, $"Card{index + 1}", index * (CardWidth + CardGap), 0f,
                CardWidth, CardHeight, $"MOD {index + 1:00}", fonts.Label);
            var body = window.Body;
            var width = CardWidth - 2f * Inset;
            WindowKit.Label(body, "Name", mod.DisplayName, Inset, 13f, width, 30f, 24, WindowKit.Ink, fonts.Heading);

            // The amount is set as a heading and what it changes as a label after it.
            var effect = mod.Effect.ToUpperInvariant();
            var split = effect.IndexOf(' ');
            var amount = WindowKit.Label(body, "Effect", split < 0 ? effect : effect.Substring(0, split), Inset, 54f,
                width, 30f, 25, WindowKit.Ink, fonts.Heading);
            if (split >= 0)
                WindowKit.Label(body, "Unit", effect.Substring(split + 1), Inset + amount.preferredWidth + 8f, 58f,
                    width, 26f, 16, WindowKit.Ink, fonts.Label);

            WindowKit.Rule(body, Inset, 121f, width);
            WindowKit.Captioned(body, "Landing", "ON", landing.ToUpperInvariant(), Inset, 134f, width, 16,
                fonts.Label);
            ScreenKit.Button((RectTransform)window.transform, () => Choose(index), WindowKit.Lit(layers, ScreenKit.Hover));
            window.Play();
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

            var skip = new InputAction("Skip", InputActionType.Button, "<Keyboard>/escape");
            skip.AddBinding("<Gamepad>/buttonSouth");
            skip.performed += _ => Choose(Skip);
            skip.Enable();
            actions.Add(skip);
        }
    }
}
