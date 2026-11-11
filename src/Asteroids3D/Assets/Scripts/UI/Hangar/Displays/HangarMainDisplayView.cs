using System;
using System.Collections.Generic;
using Ships;
using UI.Hangar.Selection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    /// <summary>
    /// The hull diagram: the selected ship's silhouette ringed by its six stat bars, its two
    /// weapons and its keystone. The three slots are buttons under that diagram, and the
    /// selected one wears a bracket.
    /// </summary>
    public sealed class HangarMainDisplayView : MonoBehaviour
    {
        public const string SilhouetteFolder = "Hangar/Ships";
        private static readonly Color Hull = Color.white;
        private static readonly Color LockedHull = new(1f, 1f, 1f, .3f);

        [SerializeField] private Text title;
        [SerializeField] private Text lockTag;
        [SerializeField] private RawImage silhouette;
        [SerializeField] private HangarStatBar[] bars;
        [SerializeField] private Button[] buttons;
        [SerializeField] private GameObject[] brackets;
        [SerializeField] private Image[] glyphs;
        [SerializeField] private Text[] names;
        [SerializeField] private Image keystoneIcon;
        [SerializeField] private Text keystoneName;
        [SerializeField] private Text keystoneCooldown;

        private readonly Dictionary<Ship, Texture2D> silhouettes = new();
        private HangarSelection selection;

        internal Button this[HangarSlot slot] => buttons[(int)slot];
        internal HangarStatBar Bar(HangarStat stat) => bars[(int)stat];
        internal string Title => title.text;
        internal bool Locked => lockTag.gameObject.activeSelf;
        internal bool Bracketed(HangarSlot slot) => brackets[(int)slot].activeSelf;

        private void Awake()
        {
            if (!title || !lockTag || !silhouette || !keystoneIcon || !keystoneName || !keystoneCooldown ||
                bars == null || bars.Length != HangarStats.Count || Array.Exists(bars, bar => !bar) ||
                buttons == null || buttons.Length != 3 || Array.Exists(buttons, button => !button) ||
                brackets == null || brackets.Length != 3 || Array.Exists(brackets, bracket => !bracket) ||
                glyphs == null || glyphs.Length != 2 || Array.Exists(glyphs, glyph => !glyph) ||
                names == null || names.Length != 2 || Array.Exists(names, text => !text))
                throw new InvalidOperationException(
                    "HangarMainDisplayView needs its title, lock tag, silhouette, six bars, three slot buttons and brackets, two weapon glyphs and names, and the keystone's icon, name and cooldown.");
        }

        public void Bind(HangarSelection selection)
        {
            this.selection = selection;
            foreach (var ship in selection.Ships) silhouettes[ship] = Silhouette(ship);
            if (selection.Ship && !silhouettes.ContainsKey(selection.Ship))
                silhouettes[selection.Ship] = Silhouette(selection.Ship);
            for (var i = 0; i < 3; i++)
            {
                var slot = (HangarSlot)i;
                buttons[i].onClick.AddListener(() => selection.Select(slot));
            }
            selection.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.Changed -= Refresh;
        }

        private void Refresh()
        {
            var ship = selection.Ship;
            title.text = ship ? ship.name : "None";
            lockTag.gameObject.SetActive(selection.ShipLocked);
            silhouette.enabled = ship;
            silhouette.color = selection.ShipLocked ? LockedHull : Hull;
            if (ship)
            {
                silhouette.texture = silhouettes[ship];
                for (var i = 0; i < HangarStats.Count; i++)
                {
                    var stat = (HangarStat)i;
                    var value = HangarStats.Value(ship, stat);
                    bars[i].Show(Number(value, stat), HangarStats.Fill(value, HangarStats.Best(selection.Ships, stat)));
                }
            }
            Glyph(0, selection.Weapon ? selection.Weapon.HudIcon : null, selection.Label(HangarSlot.Weapon));
            Glyph(1, selection.Ordnance ? selection.Ordnance.HudIcon : null, selection.Label(HangarSlot.Ordnance));
            var keystone = ship ? ship.KeystonePrefab : null;
            keystoneIcon.enabled = keystone;
            keystoneIcon.sprite = keystone ? keystone.Icon : null;
            keystoneName.text = keystone ? keystone.DisplayName : "";
            keystoneCooldown.text = keystone ? $"{keystone.Cooldown:0.#}<size=24> s</size>" : "";
            for (var i = 0; i < 3; i++)
                brackets[i].SetActive((HangarSlot)i == selection.Selected);
        }

        private void Glyph(int index, Sprite sprite, string label)
        {
            glyphs[index].enabled = sprite;
            glyphs[index].sprite = sprite;
            names[index].text = label;
        }

        private static string Number(float value, HangarStat stat)
        {
            var unit = HangarStats.Unit(stat);
            return unit.Length == 0
                ? HangarStats.Number(value)
                : $"{HangarStats.Number(value)}<size=22><color=#9FB0BF>{unit}</color></size>";
        }

        private static Texture2D Silhouette(Ship ship)
        {
            var texture = Resources.Load<Texture2D>(SilhouetteFolder + "/" + ship.name);
            if (!texture) throw new InvalidOperationException($"No hangar silhouette for '{ship.name}'.");
            return texture;
        }
    }
}
