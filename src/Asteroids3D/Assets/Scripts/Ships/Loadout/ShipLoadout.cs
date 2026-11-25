using Combat.Weapons;

namespace Ships.Loadout
{
    /// <summary>
    /// The pending selection a hangar edits before it is applied to a ship; the engine and shield
    /// come with the chassis (<see cref="Ship.Engine"/>, <see cref="Ship.Shield"/>), never slots.
    /// <see cref="Ship.Reequip"/> installs the weapons. Kept separate from the ship so the hangar can
    /// stage a build without touching the live ship until the player commits (Launch).
    /// </summary>
    public class ShipLoadout
    {
        /// <summary>The chosen ship prefab (the chassis/archetype). Changing it is a whole-player
        /// rebuild, not a data re-resolve — see <c>PlayerRig.ApplyLoadout</c>.</summary>
        public Ship Ship;

        /// <summary>Weapon prefab for each mount; null = empty slot (unarmed is a valid build).</summary>
        public WeaponComponent PrimaryWeapon;
        public WeaponComponent SecondaryWeapon;

        public ShipLoadout(Ship ship, WeaponComponent primaryWeapon, WeaponComponent secondaryWeapon)
        {
            Ship = ship;
            PrimaryWeapon = primaryWeapon;
            SecondaryWeapon = secondaryWeapon;
        }
    }
}
