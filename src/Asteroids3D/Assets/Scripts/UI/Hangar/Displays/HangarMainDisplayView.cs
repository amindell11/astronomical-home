using System;
using System.Collections.Generic;
using Ships;
using UI.Hangar.Selection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    /// <summary>
    /// The hull diagram: the selected ship's silhouette ringed by its six stat bars and its
    /// keystone. The ship is the button under that diagram; while it is selected it wears a
    /// bracket and a pair of arrows, which ask for a step through <see cref="ArrowPressed"/>.
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
        [SerializeField] private Button shipButton;
        [SerializeField] private GameObject shipBracket;
        [SerializeField] private Button previousArrow;
        [SerializeField] private Button nextArrow;
        [SerializeField] private Image keystoneIcon;
        [SerializeField] private Text keystoneName;
        [SerializeField] private Text keystoneCooldown;

        private readonly Dictionary<Ship, Texture2D> silhouettes = new();
        private HangarSelection selection;

        public event Action<int> ArrowPressed;

        internal Button ShipButton => shipButton;
        internal Button Arrow(int step) => step < 0 ? previousArrow : nextArrow;
        internal HangarStatBar Bar(HangarStat stat) => bars[(int)stat];
        internal string Title => title.text;
        internal bool Locked => lockTag.gameObject.activeSelf;
        internal bool Bracketed => shipBracket.activeSelf;

        private void Awake()
        {
            if (!title || !lockTag || !silhouette || !keystoneIcon || !keystoneName || !keystoneCooldown ||
                !shipButton || !shipBracket || !previousArrow || !nextArrow ||
                bars == null || bars.Length != HangarStats.Count || Array.Exists(bars, bar => !bar))
                throw new InvalidOperationException(
                    "HangarMainDisplayView needs its title, lock tag, silhouette, six bars, the ship button, its bracket and arrows, and the keystone's icon, name and cooldown.");
        }

        public void Bind(HangarSelection selection)
        {
            this.selection = selection;
            foreach (var ship in selection.Ships) silhouettes[ship] = Silhouette(ship);
            if (selection.Ship && !silhouettes.ContainsKey(selection.Ship))
                silhouettes[selection.Ship] = Silhouette(selection.Ship);
            shipButton.onClick.AddListener(() => selection.Select(HangarSlot.Ship));
            previousArrow.onClick.AddListener(() => ArrowPressed?.Invoke(-1));
            nextArrow.onClick.AddListener(() => ArrowPressed?.Invoke(1));
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
            var keystone = ship ? ship.KeystonePrefab : null;
            keystoneIcon.enabled = keystone;
            keystoneIcon.sprite = keystone ? keystone.Icon : null;
            keystoneName.text = keystone ? keystone.DisplayName : "";
            keystoneCooldown.text = keystone ? $"{keystone.Cooldown:0.#}<size=24> s</size>" : "";
            var shipSelected = selection.Selected == HangarSlot.Ship;
            shipBracket.SetActive(shipSelected);
            previousArrow.gameObject.SetActive(shipSelected);
            nextArrow.gameObject.SetActive(shipSelected);
        }

        private static string Number(float value, HangarStat stat)
        {
            var number = value.ToString("0.#");
            var unit = HangarStats.Unit(stat);
            return unit.Length == 0 ? number : $"{number}<size=22><color=#9FB0BF>{unit}</color></size>";
        }

        private static Texture2D Silhouette(Ship ship)
        {
            var texture = Resources.Load<Texture2D>(SilhouetteFolder + "/" + ship.name);
            if (!texture) throw new InvalidOperationException($"No hangar silhouette for '{ship.name}'.");
            return texture;
        }
    }
}
