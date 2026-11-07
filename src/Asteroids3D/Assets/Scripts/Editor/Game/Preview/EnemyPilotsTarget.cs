using System;
using System.Collections.Generic;
using System.Linq;
using AI;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class EnemyPilotsTarget : TuningTarget
    {
        private const string FightSectorPath = "Assets/Prefabs/Sectors/Preview/CrowdedFightSector.prefab";

        public override string Name => "Enemy pilots";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.CrowdedFight };

        // Read through serialization so game types grow no preview member.
        public override IEnumerable<ScriptableObject> Profiles(GameHost game)
        {
            var sector = AssetDatabase.LoadAssetAtPath<Sector>(FightSectorPath);
            var waves = sector ? sector.GetComponentInChildren<WaveDirector>(true) : null;
            var spine = sector ? sector.Module<PhaseSpineModule>() : null;
            var elite = spine ? Reference<UnityEngine.Object>(spine, "elite") : null;
            if (!waves || !elite)
                throw new InvalidOperationException($"The crowded fight fixture '{FightSectorPath}' must wire waves and an elite.");

            using var roster = new SerializedObject(waves);
            var rosterPilots = roster.FindProperty("roster");
            var pilots = Enumerable.Range(0, rosterPilots.arraySize)
                .Select(i => (Component)rosterPilots.GetArrayElementAtIndex(i).FindPropertyRelative("pilot").objectReferenceValue)
                .Append(Reference<Component>(elite, "pilot"))
                .Append(Reference<Component>(elite, "escortPilot"));
            return pilots.Select(Personality).Distinct().ToArray();
        }

        private static ScriptableObject Personality(Component pilot)
        {
            var brain = pilot ? pilot.GetComponent<Brain>() : null;
            var personality = brain ? Reference<ScriptableObject>(brain, "personality") : null;
            if (!personality)
                throw new InvalidOperationException(
                    $"Every pilot in '{FightSectorPath}' must fly a brain with a personality; '{(pilot ? pilot.name : "none")}' does not.");
            return personality;
        }

        private static T Reference<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object
        {
            using var serialized = new SerializedObject(owner);
            return (T)serialized.FindProperty(field)?.objectReferenceValue;
        }
    }
}
