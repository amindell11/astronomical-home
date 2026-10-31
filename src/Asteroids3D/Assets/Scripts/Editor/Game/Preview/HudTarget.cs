using System.Collections.Generic;
using UI.Projection;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class HudTarget : TuningTarget
    {
        public override string Name => "HUD";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { CanopyProfile(game) };

        public override void DrawControls(PreviewHost host)
        {
            var projection = host.Entry == PreviewEntry.Combat ? host.Playtest.CanopyProjection : null;
            if (projection)
                projection.ForceMode = (CanopyForceMode)EditorGUILayout.EnumPopup("Force canopy state", projection.ForceMode);
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
