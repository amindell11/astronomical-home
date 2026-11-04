using System;
using Balance;
using Combat.Weapons;
using Ships;
using UnityEngine;

namespace Substrate.Sectors.Factions
{
    /// <summary>
    /// One faction's facts, and the one place sector content reads them from: the two ships it flies,
    /// the fixed kit each carries, the weapon that answers it, and the skin its ships wear. A sector
    /// names its faction through <see cref="SectorFaction"/>. The elite flies the signature ship in the
    /// skin's elite variant; the rest of what makes an elite lives with the elite profile, not here.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1320
    /// </summary>
    [CreateAssetMenu(fileName = "Faction", menuName = "Sector/Faction")]
    public class Faction : ScriptableObject
    {
        [Tooltip("Stable id other systems key on, e.g. 'shoal'.")]
        public string id;

        [Tooltip("Name shown to the player.")]
        public string displayName;

        public Color color = Color.white;

        [Tooltip("The faction's own chassis. Its elite flies it too.")]
        [Stat] public FactionShip signature;

        [Tooltip("The chassis every faction flies, in its own colours.")]
        [Stat] public FactionShip common;

        [Tooltip("The weapon that answers this faction's way of fighting.")]
        public WeaponComponent answer;

        public FactionSkin skin = new();

        public Ship EliteShip => signature.ship;

        /// <summary>The fixed primary <paramref name="chassis"/> flies for this faction.</summary>
        public WeaponComponent KitFor(Ship chassis)
        {
            var kit = chassis == signature.ship ? signature.kit : chassis == common.ship ? common.kit : null;
            if (!kit)
                throw new InvalidOperationException(
                    $"Faction '{name}' has no kit for '{(chassis ? chassis.name : "no ship")}': it flies only its signature and common ships, each with a kit set.");
            return kit;
        }

        public void Dress(Ship ship, bool elite) => skin.Dress(ship, elite);
    }

    [Serializable]
    public struct FactionShip
    {
        [Stat] public Ship ship;

        [Tooltip("The one primary this ship always flies. No secondary: AI aim leads for the primary only.")]
        [Stat] public WeaponComponent kit;
    }
}
