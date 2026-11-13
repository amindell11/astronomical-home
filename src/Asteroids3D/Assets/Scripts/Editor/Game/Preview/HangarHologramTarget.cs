using System.Collections.Generic;
using System.Linq;
using UI.Hangar.Hologram;
using UnityEditor;
using UnityEngine;

namespace Game.Preview
{
    internal sealed class HangarHologramTarget : TuningTarget
    {
        private readonly Dictionary<Material, Editor> materialEditors = new();

        public override string Name => "Hangar hologram";
        public override IReadOnlyList<PreviewEntry> Entries { get; } = new[] { PreviewEntry.Combat };
        public override IEnumerable<ScriptableObject> Profiles(GameHost game) => new ScriptableObject[] { Hologram(game) };

        public override void DrawControls(PreviewHost host)
        {
            EditorGUILayout.HelpBox("The run opens in the hangar; Restart Playtest returns to it. " +
                                    "Material edits write the asset at once.", MessageType.None);
            using var projector = Projector(host.gameHostPrefab);
            var hologram = (HangarHologramProfile)projector.FindProperty("profile").objectReferenceValue;
            var surface = (MeshRenderer)projector.FindProperty("surface").objectReferenceValue;
            var cone = hologram.ProjectorCone
                ? hologram.ProjectorCone.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                : Enumerable.Empty<Material>();
            var materials = hologram.Restyle.Counterparts.Select(counterpart => counterpart.counterpart)
                .Concat(cone)
                .Concat(surface.sharedMaterials)
                .Where(material => material).Distinct();
            foreach (var material in materials)
            {
                materialEditors.TryGetValue(material, out var editor);
                Editor.CreateCachedEditor(material, typeof(MaterialEditor), ref editor);
                materialEditors[material] = editor;
                editor.DrawHeader();
                editor.OnInspectorGUI();
            }
        }

        private static HangarHologramProfile Hologram(GameHost game)
        {
            using var projector = Projector(game);
            return (HangarHologramProfile)projector.FindProperty("profile").objectReferenceValue;
        }

        // Read through serialization, so the projector grows no member for the preview.
        private static SerializedObject Projector(GameHost game) => new(game.hangarConsolePrefab.Projector);
    }
}
