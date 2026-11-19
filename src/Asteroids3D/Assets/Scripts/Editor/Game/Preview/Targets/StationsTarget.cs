using System.Collections.Generic;
using System.Linq;
using Substrate.Sectors.Elements.Stations.Visual;
using UnityEditor;
using UnityEngine;

namespace Game.Preview.Targets
{
    internal sealed class StationsTarget : TuningTarget
    {
        public override string Name => "Stations";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => game.sessionProfile.run.Sectors
            .SelectMany(sector => sector.prefab.GetComponentsInChildren<StationMotion>(true))
            .Select(Feel)
            .Distinct();

        // Read through serialization, so the motion grows no member for the preview.
        private static ScriptableObject Feel(StationMotion motion)
        {
            using var serialized = new SerializedObject(motion);
            return (ScriptableObject)serialized.FindProperty("feel").objectReferenceValue;
        }
    }
}
