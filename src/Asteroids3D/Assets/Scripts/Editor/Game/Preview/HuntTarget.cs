using System.Collections.Generic;
using System.Linq;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Stations;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class HuntTarget : TuningTarget
    {
        public override string Name => "Hunt";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        // A fixture sector may carry neither, and then gives no profile.
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => game.sessionProfile.sectors
            .SelectMany(sector => new Object[]
            {
                sector.prefab.GetComponentInChildren<SectorHunt>(true),
                sector.prefab.GetComponentInChildren<RepairStation>(true)
            })
            .Where(owner => owner)
            .Select(Feel)
            .Distinct();

        // Read through serialization, so the hunt and the station grow no member for the preview.
        private static ScriptableObject Feel(Object owner)
        {
            using var serialized = new SerializedObject(owner);
            return (ScriptableObject)serialized.FindProperty("feel").objectReferenceValue;
        }
    }
}
