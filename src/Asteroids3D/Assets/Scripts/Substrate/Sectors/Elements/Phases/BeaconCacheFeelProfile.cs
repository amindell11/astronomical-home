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

        [Tooltip("Mod bonuses a claim may roll, among those that fit the hero; each mod's amount is its strength.")]
        [Stat] public LullMod[] bonuses = Array.Empty<LullMod>();

        [Tooltip("Fraction of a full hull the hull patch restores at once; past full is wasted.")]
        [Stat, Range(0f, 1f)] public float hullRestore = .3f;

        [Tooltip("Roll weight of the hull patch against each fitting bonus, which weighs 1; 0 never rolls it.")]
        [Stat, Min(0f)] public float hullWeight = 1f;

        [Tooltip("Lean the roll toward the hull patch while the hero's hull is low.")]
        [Stat] public bool leanToHull;

        [Tooltip("Hull fraction the hero must be below for the lean to apply.")]
        [Stat, Range(0f, 1f)] public float lowHullThreshold = .35f;

        [Tooltip("What the lean multiplies the hull patch's weight by.")]
        [Stat, Min(1f)] public float lowHullWeightMultiplier = 3f;

        /// <summary>The hull patch's roll weight for a hero at <paramref name="hullPct"/>; at the threshold exactly, no lean.</summary>
        public float HullWeight(float hullPct) =>
            leanToHull && hullPct < lowHullThreshold ? hullWeight * lowHullWeightMultiplier : hullWeight;
    }
}
