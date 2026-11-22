using Combat.Weapons;
using Ships;
using Ships.Command;
using Ships.Loadout;

namespace Substrate.Sectors.Factions
{
    public readonly struct FactionRank
    {
        public readonly Ship ship;
        public readonly WeaponComponent kit;
        public readonly Commander pilot;
        public readonly ShipScale scale;

        public ShipLoadout Loadout => new(ship, kit, null);

        public FactionRank(Ship ship, WeaponComponent kit, Commander pilot, ShipScale scale)
        {
            this.ship = ship;
            this.kit = kit;
            this.pilot = pilot;
            this.scale = scale;
        }
    }
}
