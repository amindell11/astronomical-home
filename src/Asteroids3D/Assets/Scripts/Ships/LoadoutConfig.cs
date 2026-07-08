using UnityEngine;

namespace Ships
{
    /// <summary>
    /// The catalog of modules the hangar offers per slot — the pool a <see cref="ShipLoadout"/>
    /// selection is chosen from. A static authored list for now; loot/ownership (which modules the
    /// player has actually earned) is a later layer that would filter this pool.
    /// </summary>
    [CreateAssetMenu(fileName = "LoadoutConfig", menuName = "Ship/Loadout Config")]
    public class LoadoutConfig : ScriptableObject
    {
        [Tooltip("Ship prefabs selectable in the hangar's Ship slot (the chassis choice).")]
        public Ship[] ships;

        [Tooltip("Engine modules selectable in the hangar's Engine slot.")]
        public EngineModule[] engines;

        [Tooltip("Shield modules selectable in the hangar's Shield slot.")]
        public ShieldModule[] shields;
    }
}
