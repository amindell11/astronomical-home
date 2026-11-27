using Combat.Weapons;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Presentation;
using UnityEngine;

namespace Substrate.Sectors.Factions
{
    public readonly struct FactionRank
    {
        public readonly Ship ship;
        public readonly WeaponComponent kit;
        public readonly Commander pilot;
        public readonly ShipScale scale;
        public readonly Material[] hull;
        public readonly ShipPalettes palettes;

        public ShipLoadout Loadout => new(ship, kit, null);

        public FactionRank(Ship ship, WeaponComponent kit, Commander pilot, ShipScale scale, Material[] hull,
            ShipPalettes palettes = null)
        {
            this.ship = ship;
            this.kit = kit;
            this.pilot = pilot;
            this.scale = scale;
            this.hull = hull;
            this.palettes = palettes;
        }
    }
}
