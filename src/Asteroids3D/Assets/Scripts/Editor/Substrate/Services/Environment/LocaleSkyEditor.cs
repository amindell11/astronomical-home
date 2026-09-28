using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Substrate.Services.Environment
{
    /// <summary>
    /// Makes a <see cref="LocaleSky"/> root the one place its sky layers are edited together: each
    /// child sky layer's GameObject checkbox, then its material through Unity's own
    /// <see cref="MaterialEditor"/>, so variant override bars and Revert still work.
    /// </summary>
    [CustomEditor(typeof(LocaleSky))]
    public sealed class LocaleSkyEditor : UnityEditor.Editor
    {
        private readonly Dictionary<Material, MaterialEditor> materialEditors = new();

        public override void OnInspectorGUI()
        {
            foreach (Transform child in ((LocaleSky)target).transform)
            {
                var renderer = child.GetComponent<Renderer>();
                if (!renderer)
                    continue;
                EditorGUILayout.Space();
                DrawActiveToggle(child.gameObject);
                var material = renderer.sharedMaterial;
                if (!material)
                    continue;
                var materialEditor = MaterialEditorFor(material);
                materialEditor.DrawHeader();
                materialEditor.OnInspectorGUI();
            }
        }

        private void OnDisable()
        {
            foreach (var materialEditor in materialEditors.Values)
                DestroyImmediate(materialEditor);
            materialEditors.Clear();
        }

        private static void DrawActiveToggle(GameObject layer)
        {
            var active = EditorGUILayout.ToggleLeft(layer.name, layer.activeSelf, EditorStyles.boldLabel);
            if (active == layer.activeSelf)
                return;
            Undo.RecordObject(layer, $"Toggle sky layer {layer.name}");
            layer.SetActive(active);
        }

        private MaterialEditor MaterialEditorFor(Material material)
        {
            if (materialEditors.TryGetValue(material, out var cached) && cached)
                return cached;
            var created = (MaterialEditor)CreateEditor(material);
            materialEditors[material] = created;
            return created;
        }
    }
}
