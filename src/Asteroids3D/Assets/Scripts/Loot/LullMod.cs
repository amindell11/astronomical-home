using Ships;
using UnityEngine;

namespace Loot
{
    /// <summary>
    /// One pick the lull pool can offer, kept for the run: a weapon mod reads through the carried
    /// mounts, a ship mod folds into the ship's own resolve. Both answer the card the same way, and
    /// both ride <see cref="LullModStack"/>, which the player rig binds to its ship once.
    /// </summary>
    public abstract class LullMod : ScriptableObject
    {
        [Tooltip("Name on the pick card. Empty = the asset name.")]
        [SerializeField] private string displayName;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        /// <summary>The card's effect line, read off the data so it cannot drift from it.</summary>
        public abstract string Effect { get; }

        /// <summary>Whether a pick would land anywhere on <paramref name="ship"/>.</summary>
        public abstract bool Fits(Ship ship);

        /// <summary>What a pick lands on, for the card; asked only of a mod that fits.</summary>
        public abstract string Landing(Ship ship);

#if UNITY_EDITOR
        protected void SetDisplayName(string value) => displayName = value;
#endif
    }
}
