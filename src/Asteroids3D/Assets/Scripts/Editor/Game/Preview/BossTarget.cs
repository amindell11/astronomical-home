using System;
using System.Collections.Generic;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class BossTarget : TuningTarget
    {
        private const string BossSectorPath = "Assets/Prefabs/Sectors/Preview/BossSector.prefab";

        public override string Name => "Boss";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Boss };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { Boss() };

        // Read through serialization so game types grow no preview member.
        private static EliteProfile Boss()
        {
            var sector = AssetDatabase.LoadAssetAtPath<Sector>(BossSectorPath);
            var spine = sector ? sector.Module<PhaseSpineModule>() : null;
            var encounter = spine ? Reference<EliteEncounter>(spine, "elite") : null;
            var profile = encounter ? Reference<EliteProfile>(encounter, "profile") : null;
            if (!profile)
                throw new InvalidOperationException($"The boss fixture '{BossSectorPath}' must wire an elite with a profile.");
            return profile;
        }

        private static T Reference<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object
        {
            using var serialized = new SerializedObject(owner);
            return (T)serialized.FindProperty(field).objectReferenceValue;
        }
    }
}
