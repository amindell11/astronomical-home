using Ships;
using UnityEngine;

namespace Loot
{
    /// <summary>
    /// One pick kept for the run: a behaviour mod is a key the part it lands on asks about, a weapon
    /// mod reads through the carried mounts, a ship mod folds into the ship's own resolve. All answer
    /// the card the same way and ride <see cref="LullModStack"/>, which a ship binds once.
    /// </summary>
    public abstract class LullMod : ScriptableObject
    {
        [Tooltip("Name on the pick card. Empty = the asset name.")]
        [SerializeField] private string displayName;

        [Tooltip("Badge art on the flight HUD. Empty = the name's initials.")]
        [SerializeField] private Sprite icon;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public Sprite Icon => icon;

        public abstract string Effect { get; }

        /// <summary>Whether a pick would land anywhere on <paramref name="ship"/>.</summary>
        public abstract bool Fits(Ship ship);

        /// <summary>What a pick lands on, for the card; asked only of a mod that fits.</summary>
        public abstract string Landing(Ship ship);

        /// <summary>The weapon or keystone glyph a pick lands on, or null for a ship-wide mod.</summary>
        public virtual Sprite LandingIcon(Ship ship) => null;

#if UNITY_EDITOR
        protected void SetDisplayName(string value) => displayName = value;
#endif
    }
}
