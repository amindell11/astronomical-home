using Combat.Weapons;

namespace Ships
{
    /// <summary>
    /// The pending module selection a hangar edits before it is applied to a ship. Holds every
    /// first-class slot — chassis, engine, shield, and the two weapon mounts;
    /// <see cref="Ship.Reequip"/> is what actually installs them. Kept separate from the ship so
    /// the hangar can stage a build without touching the live ship until the player commits
    /// (Launch).
    ///
    /// Engine/Shield modules are ScriptableObjects; weapon modules are the weapon prefabs
    /// themselves (prefab-as-module, like the chassis slot) — the loadout model treats all slots
    /// uniformly even though their install paths differ (data re-resolve vs mount re-instantiation).
    /// </summary>
    public class ShipLoadout
    {
        /// <summary>The chosen ship prefab (the chassis/archetype). Changing it is a whole-player
        /// rebuild, not a data re-resolve — see <c>SessionRig.ApplyLoadout</c>.</summary>
        public Ship Ship;

        public EngineModule Engine;
        public ShieldModule Shield;

        /// <summary>Weapon prefab for each mount; null = empty slot (unarmed is a valid build).</summary>
        public WeaponComponent PrimaryWeapon;
        public WeaponComponent SecondaryWeapon;

        public ShipLoadout(Ship ship, EngineModule engine, ShieldModule shield,
            WeaponComponent primaryWeapon, WeaponComponent secondaryWeapon)
        {
            Ship = ship;
            Engine = engine;
            Shield = shield;
            PrimaryWeapon = primaryWeapon;
            SecondaryWeapon = secondaryWeapon;
        }
    }
}
