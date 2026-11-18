using System;
using Combat.Weapons;
using UI.Hangar.Selection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hangar.Displays
{
    /// <summary>
    /// One right-hand weapons screen showing one slot's pick. Its arrows ask for a step
    /// through <see cref="ArrowPressed"/>.
    /// </summary>
    public sealed class HangarWeaponDisplayView : MonoBehaviour
    {
        private const float LockedAlpha = .35f;

        [SerializeField] private HangarSlot slot;
        [SerializeField] private Text header;
        [SerializeField] private Text title;
        [SerializeField] private Image icon;
        [SerializeField] private Text description;
        [SerializeField] private GameObject[] tags;
        [SerializeField] private Text[] tagLabels;
        [SerializeField] private Text damage;
        [SerializeField] private Text damageCaption;
        [SerializeField] private GameObject bracket;
        [SerializeField] private Button select;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private CanvasGroup content;

        private HangarSelection selection;

        public event Action<int> ArrowPressed;

        internal string Title => title.text;
        internal string Description => description.text;
        internal string Damage => damage.text;
        internal string DamageCaption => damageCaption.text;
        internal bool Bracketed => bracket.activeSelf;
        internal bool Dimmed => content.alpha < 1f;
        internal int TagCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < tags.Length; i++)
                    if (tags[i].activeSelf) count++;
                return count;
            }
        }
        internal string Tag(int index) => tagLabels[index].text;
        internal Button Arrow(int step) => step < 0 ? previous : next;
        internal Button Select => select;

        private void Awake()
        {
            if (slot == HangarSlot.Ship || !header || !title || !icon || !description || !damage ||
                !damageCaption || !bracket || !select || !previous || !next || !content ||
                tags == null || tagLabels == null || tags.Length == 0 || tags.Length != tagLabels.Length ||
                Array.Exists(tags, tag => !tag) || Array.Exists(tagLabels, label => !label))
                throw new InvalidOperationException(
                    "HangarWeaponDisplayView needs a weapon or ordnance slot, its header, title, icon, description, matching tags, damage readout, bracket, select and arrow buttons, and content.");
        }

        public void Bind(HangarSelection selection)
        {
            this.selection = selection;
            select.onClick.AddListener(() => selection.Select(slot));
            previous.onClick.AddListener(() => ArrowPressed?.Invoke(-1));
            next.onClick.AddListener(() => ArrowPressed?.Invoke(1));
            selection.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.Changed -= Refresh;
        }

        private void Refresh()
        {
            var pick = Pick();
            title.text = selection.Label(slot);
            var sprite = pick ? pick.HudIcon : null;
            icon.enabled = sprite;
            icon.sprite = sprite;
            description.text = pick ? pick.HangarDescription : "";
            var authored = pick ? pick.HangarTags.Count : 0;
            if (authored > tags.Length)
                throw new InvalidOperationException(
                    $"{pick.DisplayName} has more hangar tags than {name} can show.");
            for (var i = 0; i < tags.Length; i++)
            {
                var shown = i < authored;
                tags[i].SetActive(shown);
                if (shown) tagLabels[i].text = pick.HangarTags[i].ToUpperInvariant();
            }
            if (pick)
            {
                var (number, caption) = HangarDescriptions.Damage(pick);
                damage.text = number;
                damageCaption.text = caption;
            }
            else
            {
                damage.text = "";
                damageCaption.text = "";
            }
            bracket.SetActive(selection.Selected == slot);
            content.alpha = selection.ShipLocked ? LockedAlpha : 1f;
        }

        private WeaponComponent Pick() => slot switch
        {
            HangarSlot.Weapon => selection.Weapon,
            HangarSlot.Ordnance => selection.Ordnance,
            _ => throw new InvalidOperationException($"{name} cannot show {slot}.")
        };
    }
}
