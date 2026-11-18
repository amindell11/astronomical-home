using System.Collections.Generic;
using UI.Hangar.Hardware;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class HangarLightTarget : TuningTarget
    {
        public override string Name => "Hangar light";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };

        public override IEnumerable<ScriptableObject> Profiles(GameHost game) =>
            new ScriptableObject[] { Profile(game) };

        public override void DrawControls(PreviewHost host) =>
            EditorGUILayout.HelpBox("The run opens in the hangar; Restart Playtest returns to it.", MessageType.None);

        // Read through serialization, so the console grows no member for the preview.
        private static HangarLightProfile Profile(GameHost game)
        {
            using var console = new SerializedObject(game.hangarConsolePrefab);
            return (HangarLightProfile)console.FindProperty("lightProfile").objectReferenceValue;
        }
    }
}
