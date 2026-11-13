using System.Collections.Generic;
using UI.Hangar.Displays;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class HangarDisplayTarget : TuningTarget
    {
        public override string Name => "Hangar displays";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game) =>
            new ScriptableObject[] { Profile(game) };

        public override void DrawControls(PreviewHost host) =>
            EditorGUILayout.HelpBox("The run opens in the hangar; Restart Playtest returns to it.", MessageType.None);

        // Read through serialization, so the console grows no member for the preview.
        private static HangarDisplayProfile Profile(GameHost game)
        {
            using var console = new SerializedObject(game.hangarConsolePrefab);
            return (HangarDisplayProfile)console.FindProperty("displayProfile").objectReferenceValue;
        }
    }
}
