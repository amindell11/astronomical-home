using Balance;
using Combat.Weapons;
using UnityEngine;

namespace Ships.Loadout
{
    /// <summary>
    /// An authored subset of the <see cref="ItemCatalog"/>: the items one consumer chooses from, per
    /// loadout slot. The hangar's offer uses it: the player picks a <see cref="ShipLoadout"/> from
    /// it, and the weapon pick between sectors draws from its weapons. The hangar shows every ship but
    /// launches only the starter or a ship the player has earned.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemSubset", menuName = "Ship/Item Subset")]
    public class ItemSubset : ScriptableObject
    {
        [Tooltip("The ship a new player flies; every other offered ship unlocks by beating its elite.")]
        public Ship starter;

        [Tooltip("Ship prefabs selectable in the hangar's Ship slot (the chassis choice).")]
        [Stat] public Ship[] ships;

        [Tooltip("Weapon prefabs selectable in either weapon slot — one shared pool; the two mounts " +
                 "are identical hardware, only their trigger bindings differ.")]
        [Stat] public WeaponComponent[] weapons;
    }
}
