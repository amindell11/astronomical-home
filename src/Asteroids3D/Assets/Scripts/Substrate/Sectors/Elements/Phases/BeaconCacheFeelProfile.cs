using System;
using Balance;
using Loot;
using UnityEngine;

namespace Substrate.Sectors.Elements.Phases
{
    [CreateAssetMenu(fileName = "BeaconCacheFeel", menuName = "Sectors/Beacon Cache Feel Profile")]
    public sealed class BeaconCacheFeelProfile : ScriptableObject
    {
        [Tooltip("Seconds a claimed bonus lasts.")]
        [Stat, Min(0f)] public float cacheSeconds = 20f;

        [Tooltip("Bonuses a beacon claim rolls one of, among those that fit the hero; each mod's amount is its strength.")]
        [Stat] public LullMod[] bonuses = Array.Empty<LullMod>();
    }
}
