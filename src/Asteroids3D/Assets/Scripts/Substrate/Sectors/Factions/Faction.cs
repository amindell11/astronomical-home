using System;
using Balance;
using Combat.Weapons;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Presentation;
using Substrate.Sectors.Elements.Stations.Visual;
using UnityEngine;
using UnityEngine.Serialization;

namespace Substrate.Sectors.Factions
{
    /// <summary>
    /// One faction's facts, and the one place sector content reads them from: one base ship flown at
    /// three ranks (minion, rival, elite), the skin every rank wears, and the dress its sectors' find
    /// stations wear. A sector names its faction through <see cref="SectorFaction"/>. The elite flies
    /// the signature ship in the skin's elite set; the rest of what makes an elite lives with the elite
    /// profile, not here.
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

        [Tooltip("The signature ship, and how the rival flies it. Every rank flies this ship and wears this paint.")]
        [Stat] public FactionShip signature;

        [FormerlySerializedAs("common")]
        [Stat] public FactionMinion minion;

        [Header("Skin")]
        [Tooltip("Thrust flame every faction ship wears. Unset → today's flame.")]
        public Material flame;

        [Tooltip("Thrust flame the elite wears. Unset → today's flame.")]
        public Material eliteFlame;

        [Tooltip("Shield ring colour every faction ship wears.")]
        public Color shieldRing = new(0.51f, 0.75f, 0.76f, 1f);

        [Header("Stations")]
        [Tooltip("How the find stations in this faction's sectors are dressed: a dressed model for each kind and the palettes they roll.")]
        public StationDress stationDress;

        public Ship Ship => signature.ship;

        public FactionRank Rival
        {
            get
            {
                Require(signature.ship, "signature ship");
                Require(signature.kit, "rival kit");
                Require(signature.pilot, "rival pilot");
                return new FactionRank(signature.ship, signature.kit, signature.pilot, ShipScale.One);
            }
        }

        public FactionRank Minion
        {
            get
            {
                Require(signature.ship, "signature ship");
                Require(minion.kit, "minion kit");
                Require(minion.pilot, "minion pilot");
                if (minion.scale == null)
                    throw new InvalidOperationException($"Faction '{name}' sets no minion scale.");
                return new FactionRank(signature.ship, minion.kit, minion.pilot, minion.scale);
            }
        }

        /// <summary>Puts the faction's skin on <paramref name="ship"/>. The elite wears the elite set.</summary>
        public void Dress(Ship ship, bool elite)
        {
            ship.VisualRig.Wear(new ShipLivery(elite ? signature.eliteHull : signature.hull, elite ? eliteFlame : flame,
                shieldRing));
        }

        private void Require(UnityEngine.Object value, string field)
        {
            if (value) return;
            throw new InvalidOperationException($"Faction '{name}' sets no {field}.");
        }
    }

    [Serializable]
    public struct FactionShip
    {
        [Stat] public Ship ship;

        [Tooltip("The one primary this ship always flies. No secondary: AI aim leads for the primary only.")]
        [Stat] public WeaponComponent kit;

        [Tooltip("The pilot this hull flies for the faction; its temper is how the faction flies it.")]
        [Stat] public Commander pilot;

        [Tooltip("Every hull material slot, in silhouette order, in faction paint. Empty → today's paint.")]
        public Material[] hull;

        [Tooltip("The same slots in the elite's paint, swapped whole. Empty → today's paint.")]
        public Material[] eliteHull;
    }

    [Serializable]
    public struct FactionMinion
    {
        [Tooltip("The one primary this minion always flies. No secondary: AI aim leads for the primary only.")]
        [Stat] public WeaponComponent kit;

        [Tooltip("The minion's pilot. It never fires the ship's keystone.")]
        [Stat] public Commander pilot;

        [Tooltip("Size and hull factors on the signature ship.")]
        public ShipScale scale;
    }
}
