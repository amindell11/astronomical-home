using System.Collections.Generic;
using Game.Player;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class FlightAssistTarget : TuningTarget
    {
        public override string Name => "Flight assist";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { Profile(game) };

        // Read through serialization, so the rig and the commander grow no member for the preview.
        private static FlightAssistProfile Profile(GameHost game)
        {
            using var rig = new SerializedObject(game.playerRig);
            using var commander = new SerializedObject(rig.FindProperty("playerCommander").objectReferenceValue);
            return (FlightAssistProfile)commander.FindProperty("assist").objectReferenceValue;
        }
    }
}
