using System;
using Balance;
using Combat.Weapons;
using Ships;
using Ships.Presentation;
using UnityEngine;

namespace Substrate.Sectors.Factions
{
    /// <summary>
    /// One faction's facts, and the one place sector content reads them from: the two ships it flies,
    /// the fixed kit each carries, and the skin its ships wear. A sector
    /// names its faction through <see cref="SectorFaction"/>. The elite flies the signature ship in the
    /// skin's elite set; the rest of what makes an elite lives with the elite profile, not here.
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

        [Header("Skin")]
        [Tooltip("Thrust flame every faction ship wears. Unset → today's flame.")]
        public Material flame;

        [Tooltip("Thrust flame the elite wears. Unset → today's flame.")]
        public Material eliteFlame;

        [Tooltip("Shield ring colour every faction ship wears.")]
        public Color shieldRing = new(0.51f, 0.75f, 0.76f, 1f);

        public Ship EliteShip => signature.ship;

        /// <summary>The fixed primary <paramref name="chassis"/> flies for this faction.</summary>
        public WeaponComponent KitFor(Ship chassis)
        {
            var kit = Entry(chassis).kit;
            if (!kit)
                throw new InvalidOperationException($"Faction '{name}' sets no kit for '{chassis.name}'.");
            return kit;
        }

        /// <summary>Puts the faction's skin on <paramref name="ship"/>, spawned from <paramref name="chassis"/>; the elite wears the elite set.</summary>
        public void Dress(Ship chassis, Ship ship, bool elite)
        {
            var entry = Entry(chassis);
            ship.VisualRig.Wear(new ShipLivery(elite ? entry.eliteHull : entry.hull, elite ? eliteFlame : flame,
                shieldRing));
        }

        private FactionShip Entry(Ship chassis)
        {
            if (chassis && chassis == signature.ship) return signature;
            if (chassis && chassis == common.ship) return common;
            throw new InvalidOperationException(
                $"Faction '{name}' doesn't fly '{(chassis ? chassis.name : "no ship")}': only its signature and common ships.");
        }
    }

    [Serializable]
    public struct FactionShip
    {
        [Stat] public Ship ship;

        [Tooltip("The one primary this ship always flies. No secondary: AI aim leads for the primary only.")]
        [Stat] public WeaponComponent kit;

        [Tooltip("Every hull material slot, in silhouette order, in faction paint. Empty → today's paint.")]
        public Material[] hull;

        [Tooltip("The same slots in the elite's paint, swapped whole. Empty → today's paint.")]
        public Material[] eliteHull;
    }
}
