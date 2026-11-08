using Balance;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    [CreateAssetMenu(fileName = "WeaponCacheFeel", menuName = "Sectors/Weapon Cache Feel Profile")]
    public sealed class WeaponCacheFeelProfile : ScriptableObject
    {
        [Tooltip("Weapon caches each sector places, each claimed once.")]
        [Stat, Min(0)] public int cachesPerSector = 2;
    }
}
