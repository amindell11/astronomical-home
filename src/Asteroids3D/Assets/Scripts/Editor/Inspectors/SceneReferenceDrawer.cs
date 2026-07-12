using System.IO;
using Game.Sectors;
using UnityEditor;
using UnityEngine;

namespace Game.Sectors.Inspectors
{
    /// <summary>
    /// Draws a <see cref="SceneReference"/> as a SceneAsset object field, baking the chosen asset's
    /// name/path into the serialized strings each draw so a rename or move can't leave them stale, and
    /// warning when the referenced scene is missing from Build Settings (it could not load at runtime).
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public class SceneReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var assetProp = property.FindPropertyRelative("sceneAsset");
            var nameProp = property.FindPropertyRelative("sceneName");

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, assetProp, label);

            var asset = assetProp.objectReferenceValue as SceneAsset;
            var path = asset ? AssetDatabase.GetAssetPath(asset) : string.Empty;
            var sceneName = asset ? Path.GetFileNameWithoutExtension(path) : string.Empty;
            if (nameProp.stringValue != sceneName) nameProp.stringValue = sceneName;

            if (asset && !InEnabledBuildSettings(path))
            {
                var warn = new Rect(position.x, line.yMax + Spacing, position.width, WarnHeight);
                EditorGUI.HelpBox(warn,
                    $"'{sceneName}' is not enabled in Build Settings — it will not load additively at runtime.",
                    MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var asset = property.FindPropertyRelative("sceneAsset").objectReferenceValue as SceneAsset;
            var height = EditorGUIUtility.singleLineHeight;
            if (asset && !InEnabledBuildSettings(AssetDatabase.GetAssetPath(asset)))
                height += Spacing + WarnHeight;
            return height;
        }

        private const float Spacing = 2f;
        private static float WarnHeight => EditorGUIUtility.singleLineHeight * 2f;

        private static bool InEnabledBuildSettings(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled && scene.path == path) return true;
            return false;
        }
    }
}
