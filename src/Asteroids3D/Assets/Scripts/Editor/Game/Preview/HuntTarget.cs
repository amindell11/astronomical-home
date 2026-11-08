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

        // A sector may lack a hunt, a station or a cache; each missing one gives no profile.
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => game.sessionProfile.run.Sectors
            .SelectMany(sector => new (Object owner, string field)[]
            {
                (sector.prefab.GetComponentInChildren<SectorHunt>(true), "feel"),
                (sector.prefab.GetComponentInChildren<RepairStation>(true), "feel"),
                (sector.prefab.GetComponentInChildren<WeaponCache>(true), "feel"),
                (sector.prefab.GetComponentInChildren<WeaponCache>(true), "boost")
            })
            .Where(entry => entry.owner)
            .Select(entry => Profile(entry.owner, entry.field))
            .Distinct();

        // Read through serialization, so the profile owners grow no member for the preview.
        private static ScriptableObject Profile(Object owner, string field)
        {
            using var serialized = new SerializedObject(owner);
            return (ScriptableObject)serialized.FindProperty(field).objectReferenceValue;
        }
    }
}
