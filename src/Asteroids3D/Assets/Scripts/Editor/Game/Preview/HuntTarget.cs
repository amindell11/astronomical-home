using System.Collections.Generic;
using System.Linq;
using Substrate.Sectors.Elements.Hunt;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class HuntTarget : TuningTarget
    {
        public override string Name => "Hunt";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => game.sessionProfile.sectors
            .Select(sector => Feel(sector.prefab.GetComponentInChildren<SectorHunt>(true)))
            .Distinct();

        // Read through serialization, so the hunt grows no member for the preview.
        private static ScriptableObject Feel(SectorHunt hunt)
        {
            using var serialized = new SerializedObject(hunt);
            return (ScriptableObject)serialized.FindProperty("feel").objectReferenceValue;
        }
    }
}
