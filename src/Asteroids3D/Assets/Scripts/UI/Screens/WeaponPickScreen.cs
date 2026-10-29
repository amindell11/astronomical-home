using System;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Ships.Command;
using Substrate.Sectors.Elements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.Screens
{
    /// <summary>
    /// The sector-break weapon pick's uGUI, in two steps: up to three weapon cards, each with the held
    /// mods it would use, then the mount it replaces, each with the held mods that swap would idle. The
    /// next sector's ships and weapons head the screen. Keyed (1, 2, 3, Space) and clickable, answered
    /// through <see cref="Choice"/> and <see cref="Slot"/>; the host holds the clock and applies the swap.
    /// How it was ruled to read: https://github.com/amindell11/astronomical-home/issues/1224#issuecomment-6475710068
    /// </summary>
    public sealed class WeaponPickScreen : MonoBehaviour
    {
        public const int MaxOffers = 3;
        public const int Skip = -1;

        private static readonly Color Ink = new(.93f, .94f, .93f);
        private static readonly Color Muted = new(.70f, .74f, .81f);
        private static readonly Color Line = new(.84f, .87f, .93f, .25f);
        private static readonly Color Paint = new(.91f, .91f, .85f);
        private static readonly Color Gain = new(.56f, .83f, .67f);
        private static readonly Color Loss = new(.91f, .62f, .52f);
        private static readonly Color Backing = new(.047f, .078f, .153f, .9f);
        private static readonly Color Dim = new(.047f, .078f, .153f, .55f);
        private static readonly Color Hover = new(1f, 1f, 1f, .08f);
        private static readonly string[] Keys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3" };
        private static readonly string[] PadButtons =
            { "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth", "<Gamepad>/buttonEast" };
        private static readonly WeaponSlot[] Slots = { WeaponSlot.Primary, WeaponSlot.Secondary };
        private const float CardWidth = 300f;
        private const float CardHeight = 190f;
        private const float CardGap = 24f;

        private readonly List<InputAction> actions = new();
        private Font font;
        private IReadOnlyList<WeaponComponent> offers;
        private WeaponComponent primary;
        private WeaponComponent secondary;
        private WeaponModStack mods;
        private RectTransform offerStep;
        private RectTransform mountStep;
        private int? offered;

        /// <summary>Null until answered; then the picked offer's index, or <see cref="Skip"/>.</summary>
        public int? Choice { get; private set; }

        /// <summary>The mount the picked offer replaces; meaningful once <see cref="Choice"/> holds an offer.</summary>
        public WeaponSlot Slot { get; private set; }

        public static WeaponPickScreen Create(Transform parent, int sector, SectorThreat? threat,
            IReadOnlyList<WeaponComponent> offers, WeaponComponent primary, WeaponComponent secondary,
            WeaponModStack mods)
        {
            if (offers == null || offers.Count == 0 || offers.Count > MaxOffers)
                throw new ArgumentException($"A weapon pick offers one to {MaxOffers} weapons.", nameof(offers));
            var go = new GameObject("WeaponPickScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
            var screen = go.AddComponent<WeaponPickScreen>();
            screen.offers = offers;
            screen.primary = primary;
            screen.secondary = secondary;
            screen.mods = mods ?? throw new ArgumentNullException(nameof(mods));
            screen.Build(sector, threat);
            screen.BindKeys();
            return screen;
        }

        public void ChooseOffer(int index)
        {
            if (index < 0 || index >= offers.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Not an offered card.");
            if (Choice.HasValue || offered.HasValue) return;
            offered = index;
            BuildMountStep(offers[index]);
            offerStep.gameObject.SetActive(false);
        }

        public void ChooseSlot(WeaponSlot slot)
        {
            if (Choice.HasValue || !offered.HasValue) return;
            Slot = slot;
            Choice = offered;
        }

        public void SkipOrBack()
        {
            if (Choice.HasValue) return;
            if (!offered.HasValue)
            {
                Choice = Skip;
                return;
            }
            offered = null;
            Destroy(mountStep.gameObject);
            mountStep = null;
            offerStep.gameObject.SetActive(true);
        }

        private void Awake() => font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            actions.Clear();
        }

        private void Build(int sector, SectorThreat? threat)
        {
            var dim = Centered(transform, "Dim", 0f, 0f, 0f);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            dim.gameObject.AddComponent<Image>().color = Dim;

            Label(Centered(transform, "Title", 600f, 48f, 236f), "WEAPON PICK", 40, Paint, TextAnchor.MiddleCenter);
            Label(Centered(transform, "Next", 1000f, 18f, 196f), $"SECTOR {sector:00} SENDS", 11, Muted,
                TextAnchor.MiddleCenter);
            if (threat is SectorThreat sends)
            {
                Label(Centered(transform, "Ships", 1000f, 20f, 176f),
                    "SHIPS   " + Names(sends.Ships, ship => ship.name), 13, Ink, TextAnchor.MiddleCenter);
                Label(Centered(transform, "Weapons", 1000f, 20f, 156f),
                    "WEAPONS   " + Names(sends.Weapons, weapon => weapon.DisplayName), 13, Ink, TextAnchor.MiddleCenter);
            }

            offerStep = Centered(transform, "Offers", 0f, 0f, 0f);
            Label(Centered(offerStep, "Hint", 600f, 20f, 120f), "PICK A WEAPON: CLICK A CARD OR PRESS ITS KEY", 12,
                Muted, TextAnchor.MiddleCenter);
            var count = offers.Count;
            var cards = Centered(offerStep, "Cards", count * CardWidth + (count - 1) * CardGap, CardHeight, 0f);
            for (var i = 0; i < count; i++)
                BuildOfferCard(cards, i, offers[i]);
            BuildKeyButton(Centered(offerStep, "Skip", 216f, 38f, -CardHeight / 2f - 44f), "SKIP", SkipOrBack);
        }

        private void BuildOfferCard(RectTransform cards, int index, WeaponComponent weapon)
        {
            var card = Card(cards, $"Card{index + 1}", index, index + 1, weapon.DisplayName, () => ChooseOffer(index));
            Label(Box(card, "UsesLabel", 18, 76, 264, 18), "USES YOUR MODS", 11, Muted);
            var uses = mods.Using(weapon);
            Label(Box(card, "Uses", 18, 98, 264, 76), uses.Count == 0 ? "NONE" : Names(uses, held => held.Label), 14,
                uses.Count == 0 ? Muted : Gain, TextAnchor.UpperLeft);
        }

        private void BuildMountStep(WeaponComponent weapon)
        {
            mountStep = Centered(transform, "Mount", 0f, 0f, 0f);
            Label(Centered(mountStep, "Hint", 600f, 20f, 120f),
                $"MOUNT {weapon.DisplayName.ToUpperInvariant()}: CLICK A SLOT OR PRESS ITS KEY", 12, Muted,
                TextAnchor.MiddleCenter);
            var cards = Centered(mountStep, "Cards", 2 * CardWidth + CardGap, CardHeight, 0f);
            for (var i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                var replaced = slot == WeaponSlot.Primary ? primary : secondary;
                var kept = slot == WeaponSlot.Primary ? secondary : primary;
                var name = slot == WeaponSlot.Primary ? "PRIMARY · LEFT MOUSE" : "SECONDARY · RIGHT MOUSE";
                var card = Card(cards, $"Card{i + 1}", i, i + 1, name, () => ChooseSlot(slot));
                Label(Box(card, "Replaces", 18, 76, 264, 18),
                    "REPLACES  " + (replaced ? replaced.DisplayName.ToUpperInvariant() : "EMPTY"), 11, Muted);
                Label(Box(card, "IdleLabel", 18, 102, 264, 18), "GOES IDLE", 11, Muted);
                var idles = mods.IdledBy(replaced, weapon, kept);
                Label(Box(card, "Idles", 18, 122, 264, 52), idles.Count == 0 ? "NOTHING" : Names(idles, held => held.Label),
                    14, idles.Count == 0 ? Muted : Loss, TextAnchor.UpperLeft);
            }
            BuildKeyButton(Centered(mountStep, "Back", 216f, 38f, -CardHeight / 2f - 44f), "BACK", SkipOrBack);
        }

        private RectTransform Card(RectTransform cards, string name, int column, int key, string title, Action pick)
        {
            var card = Box(cards, name, column * (CardWidth + CardGap), 0f, CardWidth, CardHeight);
            Fill(card, Backing);
            Clickable(card, pick);
            Fill(Box(card, "CornerTop", 0, 0, 17, 1), Line);
            Fill(Box(card, "CornerLeft", 0, 0, 1, 17), Line);

            var cap = Box(card, "KeyCap", 18, 18, 40, 40);
            Fill(Box(cap, "Top", 0, 0, 40, 1), Line);
            Fill(Box(cap, "Bottom", 0, 39, 40, 1), Line);
            Fill(Box(cap, "Left", 0, 0, 1, 40), Line);
            Fill(Box(cap, "Right", 39, 0, 1, 40), Line);
            Label(Box(cap, "Key", 0, 0, 40, 40), key.ToString(), 22, Paint, TextAnchor.MiddleCenter);
            Label(Box(card, "Name", 72, 18, 210, 40), title.ToUpperInvariant(), 18, Ink);
            return card;
        }

        private void BuildKeyButton(RectTransform button, string label, Action press)
        {
            Clickable(button, press);
            Fill(Box(button, "Top", 0, 0, 216, 1), Line);
            Fill(Box(button, "Bottom", 0, 37, 216, 1), Line);
            Fill(Box(button, "Left", 0, 0, 1, 38), Line);
            Fill(Box(button, "Right", 215, 0, 1, 38), Line);
            Label(Box(button, "Label", 14, 0, 140, 38), label, 11, Ink);
            Label(Box(button, "Key", 140, 0, 62, 38), "SPACE", 11, Muted, TextAnchor.MiddleRight);
        }

        // A full-size overlay is the click target and shows only on hover, under the card's text.
        private void Clickable(RectTransform rect, Action press)
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
            button.onClick.AddListener(() => press());
        }

        // One action per key across both steps, so a step-changing press never repeats.
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

            var space = new InputAction("SkipOrBack", InputActionType.Button, "<Keyboard>/space");
            space.AddBinding("<Gamepad>/buttonSouth");
            space.performed += _ => SkipOrBack();
            space.Enable();
            actions.Add(space);
        }

        private void PressKey(int index)
        {
            if (!offered.HasValue)
            {
                if (index < offers.Count) ChooseOffer(index);
                return;
            }
            if (index < Slots.Length) ChooseSlot(Slots[index]);
        }

        private static string Names<T>(IReadOnlyList<T> items, Func<T, string> name)
        {
            var names = new string[items.Count];
            for (var i = 0; i < items.Count; i++) names[i] = name(items[i]).ToUpperInvariant();
            return string.Join(" · ", names);
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

        private void Label(RectTransform rect, string value, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = value;
        }
    }
}
