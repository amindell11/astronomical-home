using System;
using System.Collections.Generic;
using Combat.Weapons;
using Loot;
using Ships.Command;
using Ships.Weapons;
using UI.Screens.Windows;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens
{
    /// <summary>
    /// The armory's weapon pick uGUI: up to three windows, each naming the mount it replaces (the one
    /// in the slot its kind fills), the held mods it would use and the held mods the swap would idle.
    /// Keyed (1, 2, 3, Escape) and clickable, answered through <see cref="Choice"/>; the host holds
    /// the clock and applies the swap.
    /// How it was ruled to read: https://github.com/amindell11/astronomical-home/issues/1694
    /// </summary>
    public sealed class WeaponPickScreen : MonoBehaviour
    {
        public const int MaxOffers = 3;
        public const int Skip = -1;

        private static readonly Color ChipFill = new(1f, 1f, 1f, .2f);
        private static readonly string[] Keys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3" };
        private static readonly string[] PadButtons =
            { "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth", "<Gamepad>/buttonEast" };
        private const float CardWidth = 333f;
        private const float CardHeight = 267f;
        private const float CardGap = 29f;
        private const float CardsY = -33f;
        private const float Inset = 16f;
        private const float ChipHeight = 20f;

        private readonly List<InputAction> actions = new();
        private ScreenFonts fonts;
        private ScreenLayers layers;
        private IReadOnlyList<WeaponComponent> offers;
        private WeaponComponent primary;
        private WeaponComponent secondary;
        private LullModStack mods;

        /// <summary>Null until answered; then the picked offer's index, or <see cref="Skip"/>.</summary>
        public int? Choice { get; private set; }

        public static WeaponPickScreen Create(Transform parent, IReadOnlyList<WeaponComponent> offers,
            WeaponComponent primary, WeaponComponent secondary, LullModStack mods, ScreenCameras cameras)
        {
            if (offers == null || offers.Count == 0 || offers.Count > MaxOffers)
                throw new ArgumentException($"A weapon pick offers one to {MaxOffers} weapons.", nameof(offers));
            var go = new GameObject("WeaponPickScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layers = ScreenLayers.Create(go, cameras);
            var screen = go.AddComponent<WeaponPickScreen>();
            screen.layers = layers;
            screen.offers = offers;
            screen.primary = primary;
            screen.secondary = secondary;
            screen.mods = mods ?? throw new ArgumentNullException(nameof(mods));
            screen.Build();
            screen.BindKeys();
            return screen;
        }

        public void ChooseOffer(int index)
        {
            if (index < 0 || index >= offers.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Not an offered card.");
            Choice ??= index;
        }

        public void ChooseSkip() => Choice ??= Skip;

        private void Awake() => fonts = ScreenFonts.Load();

        private void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            actions.Clear();
        }

        private void Build()
        {
            WindowKit.Fade(layers.Under, WindowKit.StrongFade);
            ScreenKit.Label(ScreenKit.Centered(transform, "Title", 800f, 44f, 227f), "WEAPON PICK", 32,
                WindowKit.Ink, fonts.Title, TextAnchor.MiddleCenter);
            ScreenKit.Label(ScreenKit.Centered(transform, "Hint", 800f, 20f, 189f), "CHOOSE A NEW WEAPON TO EQUIP", 15,
                WindowKit.Dim, fonts.Label, TextAnchor.MiddleCenter);

            var offerRow = ScreenKit.Centered(transform, "Offers", 0f, 0f, 0f);
            var count = offers.Count;
            var cards = ScreenKit.Centered(offerRow, "Cards", count * CardWidth + (count - 1) * CardGap, CardHeight,
                CardsY);
            for (var i = 0; i < count; i++)
                BuildOfferCard(cards, i, offers[i]);
            WindowKit.KeyButton(layers, ScreenKit.Centered(offerRow, "Skip", 200f, 36f, CardsY - CardHeight / 2f - 41f),
                "SKIP", "ESC", fonts.Label, ChooseSkip);
        }

        private void BuildOfferCard(RectTransform cards, int index, WeaponComponent weapon)
        {
            var fillsWeaponSlot = WeaponsController.SlotFor(weapon) == WeaponSlot.Primary;
            var replaced = fillsWeaponSlot ? primary : secondary;
            var kept = fillsWeaponSlot ? secondary : primary;
            var window = ScreenWindow.Create(cards, layers, $"Card{index + 1}", index * (CardWidth + CardGap), 0f,
                CardWidth, CardHeight, $"WEAPON {index + 1:00}", fonts.Label);
            var body = window.Body;
            var width = CardWidth - 2f * Inset;

            var icon = ScreenKit.Box(body, "Icon", Inset, 12f, 44f, 44f).gameObject.AddComponent<Image>();
            icon.sprite = weapon.HudIcon;
            icon.enabled = weapon.HudIcon;
            icon.preserveAspect = true;
            icon.color = WindowKit.Ink;
            icon.raycastTarget = false;
            WindowKit.Label(body, "Name", weapon.DisplayName, Inset + 53f, 12f, width - 53f, 44f, 24, WindowKit.Ink,
                fonts.Heading);
            WindowKit.Captioned(body, "Replaces", "REPLACES",
                replaced ? replaced.DisplayName.ToUpperInvariant() : "EMPTY", Inset, 65f, width, 14, fonts.Label);
            WindowKit.Rule(body, Inset, 91f, width);

            WindowKit.Label(body, "UsesLabel", "USES MODS", Inset, 100f, width, 20f, 14, WindowKit.Dim, fonts.Label);
            var idleY = 123f + Chips(body, "Uses", 123f, width, mods.Using(weapon), "+", "NONE", true) + 14f;
            WindowKit.Label(body, "IdleLabel", "GOES IDLE", Inset, idleY, width, 20f, 14, WindowKit.Dim, fonts.Label);
            Chips(body, "Idles", idleY + 23f, width, mods.IdledBy(replaced, weapon, kept), "−", "NOTHING", false);

            ScreenKit.Button((RectTransform)window.transform, () => ChooseOffer(index));
            window.Play();
        }

        // One outlined chip per held mod, wrapping onto further lines; returns the height they take.
        private float Chips(RectTransform body, string name, float y, float width, IReadOnlyList<HeldMod> held,
            string sign, string none, bool lit)
        {
            var row = ScreenKit.Box(body, name, Inset, y, width, ChipHeight);
            if (held.Count == 0)
            {
                WindowKit.Label(row, "None", none, 0f, 0f, width, ChipHeight, 14, WindowKit.Dim, fonts.Label);
                return ChipHeight;
            }

            var x = 0f;
            var line = 0f;
            for (var i = 0; i < held.Count; i++)
            {
                var chip = ScreenKit.Box(row, $"Chip{i + 1}", 0f, 0f, 0f, ChipHeight);
                var label = WindowKit.Label(chip, "Label", $"{sign} {held[i].Label.ToUpperInvariant()}", 8f, 0f, 0f,
                    ChipHeight, 14, lit ? WindowKit.Ink : WindowKit.Dim, fonts.Label);
                var chipWidth = label.preferredWidth + 16f;
                if (x > 0f && x + chipWidth > width)
                {
                    x = 0f;
                    line += ChipHeight + 4f;
                }
                chip.anchoredPosition = new Vector2(x, -line);
                chip.sizeDelta = new Vector2(chipWidth, ChipHeight);
                if (lit) ScreenKit.Fill(ScreenKit.Box(chip, "Fill", 0f, 0f, chipWidth, ChipHeight), ChipFill);
                WindowKit.Shape(ScreenKit.Box(chip, "Frame", 0f, 0f, chipWidth, ChipHeight),
                    lit ? WindowKit.Ink : WindowKit.Line, 0f, 1.5f);
                label.transform.SetAsLastSibling();
                x += chipWidth + 6f;
            }

            return line + ChipHeight;
        }

        private void BindKeys()
        {
            for (var i = 0; i < Keys.Length; i++)
            {
                var action = new InputAction($"Key{i + 1}", InputActionType.Button, Keys[i]);
                action.AddBinding(PadButtons[i]);
                var index = i;
                action.performed += _ => PressKey(index);
                action.Enable();
                actions.Add(action);
            }

            var skip = new InputAction("Skip", InputActionType.Button, "<Keyboard>/escape");
            skip.AddBinding("<Gamepad>/buttonSouth");
            skip.performed += _ => ChooseSkip();
            skip.Enable();
            actions.Add(skip);
        }

        private void PressKey(int index)
        {
            if (index < offers.Count) ChooseOffer(index);
        }
    }
}
