using UnityEditor;
using UnityEngine;

namespace Substrate.Services.Environment.Flat
{
    [CustomEditor(typeof(EnvironmentAuthoring))]
    public sealed class EnvironmentAuthoringInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("background"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("repeatDistance"), new GUIContent("Travel per repeat"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("viewHeightInTiles"), new GUIContent("Visible tile height"));
            EditorGUILayout.HelpBox("Background zoom response is zero. Travel per repeat is measured in world units. Palette roles supply defaults for later layer and lighting authoring; they do not recolor the baked image.", MessageType.None);
            foreach (var role in new[] { "baseColor", "primaryColor", "secondaryColor", "accentColor" })
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(role), true);
                if (GUILayout.Button("Return " + ObjectNames.NicifyVariableName(role) + " to Palette"))
                    serializedObject.FindProperty(role).FindPropertyRelative("useOverride").boolValue = false;
            }
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("Preview / Apply Background")) EnvironmentPreviewWindow.Open();
        }
    }
}
