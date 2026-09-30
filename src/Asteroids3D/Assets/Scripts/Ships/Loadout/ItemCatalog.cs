using System.Collections.Generic;
using Combat.Weapons;
using UnityEngine;

namespace Ships.Loadout
{
    /// <summary>
    /// The index of every item in the project, grouped by item type; an item is anything that fills
    /// a loadout slot. It holds references only: each item's stats stay on its own asset. One asset
    /// at <see cref="AssetPath"/>, kept complete by an EditMode test that scans the project by type.
    /// The hangar's offer and the enemy loadout pool are separate <see cref="LoadoutConfig"/> assets
    /// that list subsets and do not reference this one (#775).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Ship/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        public const string AssetPath = "Assets/Settings/Ships/ItemCatalog.asset";

        [SerializeField] private Ship[] chassis;
        [SerializeField] private EngineModule[] engines;
        [SerializeField] private ShieldModule[] shields;
        [SerializeField] private WeaponComponent[] weapons;

        public IReadOnlyList<Ship> Chassis => chassis;
        public IReadOnlyList<EngineModule> Engines => engines;
        public IReadOnlyList<ShieldModule> Shields => shields;
        public IReadOnlyList<WeaponComponent> Weapons => weapons;
    }
}
