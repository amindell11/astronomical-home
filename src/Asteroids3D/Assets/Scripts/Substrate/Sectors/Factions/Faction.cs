using System;
using System.Collections.Generic;
using Balance;
using Combat.Weapons;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Presentation;
using Substrate.Sectors.Elements.Stations.Visual;
using Substrate.Services.Locales;
using UnityEngine;
using UnityEngine.Serialization;

namespace Substrate.Sectors.Factions
{
    /// <summary>
    /// One faction's facts, and the one place sector content reads them from. Each rank (minion, rival,
    /// elite) flies its own chassis in its own hull paint; the elite's strength lives with the elite
    /// profile, which stays with the sector. A sector names its faction through <see cref="SectorFaction"/>.
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

        [Tooltip("The rival's ship, how it flies, and its paint.")]
        [Stat] public FactionShip signature;

        [FormerlySerializedAs("common")]
        [Stat] public FactionMinion minion;

        [Stat] public FactionElite elite;

        [Tooltip("The player ship killing this faction's elite unlocks.")]
        public Ship unlocks;

        [Header("Skin")]
        [Tooltip("Thrust flame every faction ship wears. Unset → today's flame.")]
        public Material flame;

        [Tooltip("Thrust flame the elite wears. Unset → today's flame.")]
        public Material eliteFlame;

        [Tooltip("Shield ring colour every faction ship wears.")]
        public Color shieldRing = new(0.51f, 0.75f, 0.76f, 1f);

        [Tooltip("The hull palette every rank wears in a locale's sectors, by palette name; each rank's palettes must hold it. " +
                 "A locale not listed, or an empty list → each chassis's own paint.")]
        public LocalePalette[] localePalettes = Array.Empty<LocalePalette>();

        [Header("Stations")]
        [Tooltip("How the find stations in this faction's sectors are dressed: a dressed model for each kind and the palettes they roll.")]
        public StationDress stationDress;

        public Ship EliteShip
        {
            get
            {
                Require(elite.ship, "elite ship");
                return elite.ship;
            }
        }

        public string EliteName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(elite.displayName))
                    throw new InvalidOperationException($"Faction '{name}' sets no elite name.");
                return elite.displayName;
            }
        }

        public Ship Unlocks
        {
            get
            {
                Require(unlocks, "unlocked ship");
                return unlocks;
            }
        }

        public FactionRank Rival
        {
            get
            {
                Require(signature.ship, "signature ship");
                Require(signature.kit, "rival kit");
                Require(signature.pilot, "rival pilot");
                return new FactionRank(signature.ship, signature.kit, signature.pilot, ShipScale.One, signature.hull,
                    signature.palettes);
            }
        }

        public FactionRank Minion
        {
            get
            {
                Require(minion.ship, "minion ship");
                Require(minion.kit, "minion kit");
                Require(minion.pilot, "minion pilot");
                if (minion.scale == null)
                    throw new InvalidOperationException($"Faction '{name}' sets no minion scale.");
                return new FactionRank(minion.ship, minion.kit, minion.pilot, minion.scale, minion.hull, minion.palettes);
            }
        }

        // locale: interim world-state seam (AGENTS.md wiring rule 3), handed down on the call that needs it.
        public void Dress(Ship ship, in FactionRank rank, LocaleSky locale) =>
            ship.VisualRig.Wear(new ShipLivery(rank.hull, flame, shieldRing,
                palette: PaletteOf(rank.palettes, locale)));

        // locale: interim world-state seam (AGENTS.md wiring rule 3), handed down on the call that needs it.
        public void DressElite(Ship ship, LocaleSky locale) =>
            ship.VisualRig.Wear(new ShipLivery(elite.hull, eliteFlame, shieldRing,
                palette: PaletteOf(elite.palettes, locale)));

        public void CheckPalettes()
        {
            foreach (var entry in localePalettes)
            {
                if (!entry.locale || string.IsNullOrWhiteSpace(entry.palette))
                    throw new InvalidOperationException($"Faction '{name}' lists a locale palette without a locale or a palette name.");
                CheckPalette(entry, "rival", signature.ship, signature.palettes);
                CheckPalette(entry, "minion", minion.ship, minion.palettes);
                CheckPalette(entry, "elite", elite.ship, elite.palettes);
            }
        }

        private void CheckPalette(LocalePalette entry, string rank, Ship ship, ShipPalettes palettes)
        {
            if (!ship) return;
            var paints = $"Faction '{name}' paints locale '{entry.locale.name}' in palette '{entry.palette}'";
            if (!palettes || !palettes.Has(entry.palette))
                throw new InvalidOperationException($"{paints}, which the {rank} ship '{ship.name}' has no set for.");
            var missing = palettes.MissingRegion(entry.palette, ship.VisualRig.Silhouette);
            if (missing != null)
                throw new InvalidOperationException(
                    $"{paints}; the {rank} ship '{ship.name}' wears region '{missing}', which that set does not paint.");
        }

        private void OnValidate() => CheckPalettes();

        private IReadOnlyList<Material> PaletteOf(ShipPalettes palettes, LocaleSky locale)
        {
            foreach (var entry in localePalettes)
            {
                if (entry.locale != locale) continue;
                if (!palettes)
                    throw new InvalidOperationException(
                        $"Faction '{name}' paints locale '{locale.name}' in palette '{entry.palette}' on a rank with no palettes.");
                return palettes.Regions(entry.palette);
            }
            return null;
        }

        private void Require(UnityEngine.Object value, string field)
        {
            if (value) return;
            throw new InvalidOperationException($"Faction '{name}' sets no {field}.");
        }
    }

    [Serializable]
    public struct LocalePalette
    {
        public LocaleSky locale;

        [Tooltip("The palette every rank wears in that locale's sectors, e.g. 'shoal-rust'.")]
        public string palette;
    }

    [Serializable]
    public struct FactionShip
    {
        [Stat] public Ship ship;

        [Tooltip("The one primary this ship always flies. No secondary: AI aim leads for the primary only.")]
        [Stat] public WeaponComponent kit;

        [Tooltip("The pilot this hull flies for the faction; its temper is how the faction flies it.")]
        [Stat] public Commander pilot;

        [Tooltip("Every hull material slot of that chassis, in silhouette order, in faction paint. Empty → the chassis's own paint.")]
        public Material[] hull;

        [Tooltip("This chassis's hull palettes, which the locale palettes pick from. Unset → no palette; no locale palette may then be listed.")]
        public ShipPalettes palettes;
    }

    [Serializable]
    public struct FactionMinion
    {
        [Tooltip("The minion's own chassis.")]
        [Stat] public Ship ship;

        [Tooltip("The one primary this minion always flies. No secondary: AI aim leads for the primary only.")]
        [Stat] public WeaponComponent kit;

        [Tooltip("The minion's pilot. It never fires the ship's keystone.")]
        [Stat] public Commander pilot;

        [Tooltip("Every hull material slot of that chassis, in silhouette order, in faction paint. Empty → the chassis's own paint.")]
        public Material[] hull;

        [Tooltip("This chassis's hull palettes, which the locale palettes pick from. Unset → no palette; no locale palette may then be listed.")]
        public ShipPalettes palettes;

        [Tooltip("Size and hull factors on the minion chassis.")]
        public ShipScale scale;
    }

    [Serializable]
    public struct FactionElite
    {
        [Tooltip("The elite's own chassis.")]
        [Stat] public Ship ship;

        [Tooltip("Every hull material slot of that chassis, in silhouette order, in faction paint. Empty → the chassis's own paint.")]
        public Material[] hull;

        [Tooltip("This chassis's hull palettes, which the locale palettes pick from. Unset → no palette; no locale palette may then be listed.")]
        public ShipPalettes palettes;

        [Tooltip("The name the elite's health bar and arrival banner show, whichever sector it ends.")]
        public string displayName;
    }
}
