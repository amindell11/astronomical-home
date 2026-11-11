using System.Collections.Generic;
using System.Linq;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Phases;
using Substrate.Sectors.Elements.Stations;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class HuntTarget : TuningTarget
    {
        public override string Name => "Hunt";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        // A sector may lack a hunt, a repair or mod station, or a beacon cache; each missing one gives no profile.
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => game.sessionProfile.run.Sectors
            .SelectMany(sector => new Object[]
            {
                sector.prefab.GetComponentInChildren<SectorHunt>(true),
                sector.prefab.GetComponentInChildren<RepairStation>(true),
                sector.prefab.GetComponentInChildren<ModStation>(true),
                sector.prefab.GetComponentInChildren<BeaconCache>(true)
            })
            .Where(owner => owner)
            .Select(Feel)
            .SelectMany(WithBonuses)
            .Distinct();

        // The cache's bonuses are mod assets, and their amounts are what the owner tunes.
        private static IEnumerable<ScriptableObject> WithBonuses(ScriptableObject feel)
        {
            var cache = feel as BeaconCacheFeelProfile;
            return cache ? cache.bonuses.Cast<ScriptableObject>().Prepend(feel) : new[] { feel };
        }

        // Read through serialization, so the profile owners grow no member for the preview.
        private static ScriptableObject Feel(Object owner)
        {
            using var serialized = new SerializedObject(owner);
            return (ScriptableObject)serialized.FindProperty("feel").objectReferenceValue;
        }
    }
}
