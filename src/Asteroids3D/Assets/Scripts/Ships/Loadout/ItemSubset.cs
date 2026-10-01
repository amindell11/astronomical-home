using Combat.Weapons;
using UnityEngine;

namespace Ships.Loadout
{
    /// <summary>
    /// An authored subset of the <see cref="ItemCatalog"/>: the items one consumer chooses from, per
    /// loadout slot. Two assets use it: the hangar's offer, which the player picks a
    /// <see cref="ShipLoadout"/> from, and the enemy loadout pool, which a wave director draws each
    /// spawn's engine, shield and weapon from (its ships stay empty). Loot/ownership (which items
    /// the player has actually earned) is a later layer that would filter the hangar's offer.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemSubset", menuName = "Ship/Item Subset")]
    public class ItemSubset : ScriptableObject
    {
        [Tooltip("Ship prefabs selectable in the hangar's Ship slot (the chassis choice).")]
        public Ship[] ships;

        [Tooltip("Engine modules selectable in the hangar's Engine slot.")]
        public EngineModule[] engines;

        [Tooltip("Shield modules selectable in the hangar's Shield slot.")]
        public ShieldModule[] shields;

        [Tooltip("Weapon prefabs selectable in either weapon slot — one shared pool; the two mounts " +
                 "are identical hardware, only their trigger bindings differ.")]
        public WeaponComponent[] weapons;
    }
}
