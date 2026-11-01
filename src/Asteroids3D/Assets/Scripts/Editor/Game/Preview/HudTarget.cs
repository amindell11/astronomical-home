using System.Collections.Generic;
using UI.Projection;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class HudTarget : TuningTarget
    {
        public override string Name => "HUD";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[]
        {
            PreviewEntry.Startup, PreviewEntry.LowHull, PreviewEntry.Damage, PreviewEntry.Extraction, PreviewEntry.Combat
        };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { CanopyProfile(game) };

        public override void DrawControls(PreviewHost host)
        {
            if (GUILayout.Button("Import tuner settings…")) CanopyTunerImportWindow.Open(CanopyProfile(host.gameHostPrefab));
        }

        // Read through serialization, so the rig grows no member for the preview.
        internal static CanopyProjectionProfile CanopyProfile(GameHost game)
        {
            using var rig = new SerializedObject(game.playerRig);
            return ((CanopyProjection)rig.FindProperty("uiCamPrefab").objectReferenceValue).Profile;
        }
    }
}
