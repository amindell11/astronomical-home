using Combat.Weapons;

namespace Ships.Loadout
{
    /// <summary>
    /// The pending module selection a hangar edits before it is applied to a ship. Holds every
    /// first-class slot — chassis, shield, and the two weapon mounts; the engine is the chassis's
    /// own (<see cref="Ship.Engine"/>), never a slot. <see cref="Ship.Reequip"/> is what actually
    /// installs the modules. Kept separate from the ship so the hangar can stage a build without
    /// touching the live ship until the player commits (Launch).
    ///
    /// The shield module is a ScriptableObject; weapon modules are the weapon prefabs themselves
    /// (prefab-as-module, like the chassis slot) — the loadout model treats all slots uniformly
    /// even though their install paths differ (data re-resolve vs mount re-instantiation).
    /// </summary>
    public class ShipLoadout
    {
        /// <summary>The chosen ship prefab (the chassis/archetype). Changing it is a whole-player
        /// rebuild, not a data re-resolve — see <c>PlayerRig.ApplyLoadout</c>.</summary>
        public Ship Ship;

        public ShieldModule Shield;

        /// <summary>Weapon prefab for each mount; null = empty slot (unarmed is a valid build).</summary>
        public WeaponComponent PrimaryWeapon;
        public WeaponComponent SecondaryWeapon;

        public ShipLoadout(Ship ship, ShieldModule shield,
            WeaponComponent primaryWeapon, WeaponComponent secondaryWeapon)
        {
            Ship = ship;
            Shield = shield;
            PrimaryWeapon = primaryWeapon;
            SecondaryWeapon = secondaryWeapon;
        }
    }
}
