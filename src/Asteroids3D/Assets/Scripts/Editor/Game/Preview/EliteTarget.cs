using System;
using System.Collections.Generic;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Phases;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class EliteTarget : TuningTarget
    {
        private readonly string name;
        private readonly string fixturePath;

        public EliteTarget(string name, PreviewEntry entry, string fixturePath)
        {
            this.name = name;
            this.fixturePath = fixturePath;
            Entries = new[] { entry };
        }

        public override string Name => name;
        public override IReadOnlyList<PreviewEntry> Entries { get; }
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { Profile() };

        // Read through serialization so game types grow no preview member.
        private EliteProfile Profile()
        {
            var sector = AssetDatabase.LoadAssetAtPath<Sector>(fixturePath);
            var spine = sector ? sector.Module<PhaseSpineModule>() : null;
            var encounter = spine ? Reference<EliteEncounter>(spine, "elite") : null;
            var profile = encounter ? Reference<EliteProfile>(encounter, "profile") : null;
            if (!profile)
                throw new InvalidOperationException($"The elite fixture '{fixturePath}' must wire an elite with a profile.");
            return profile;
        }

        private static T Reference<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object
        {
            using var serialized = new SerializedObject(owner);
            return (T)serialized.FindProperty(field).objectReferenceValue;
        }
    }
}
