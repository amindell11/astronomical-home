using System.Collections.Generic;
using System.Linq;
using UI.Hangar;
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
            var hologram = Hologram(host.gameHostPrefab);
            var materials = hologram.Counterparts.Select(counterpart => counterpart.hologram)
                .Concat(hologram.ProjectorCone.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials))
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

        private static HangarHologramProfile Hologram(GameHost game) => game.hangarScreenPrefab.hologram;
    }
}
