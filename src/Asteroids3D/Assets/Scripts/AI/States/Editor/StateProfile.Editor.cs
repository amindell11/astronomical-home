#if UNITY_EDITOR
using Movement.MPC;
using UnityEditor;
using UnityEngine;

namespace AI.States
{
    [CustomEditor(typeof(StateProfile))]
    public class StateProfileEditor : UnityEditor.Editor
    {
        private Settings cachedBaseSettings;
        private bool baseSettingsResolved;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var iterator = serializedObject.GetIterator();
            iterator.NextVisible(true); // skip m_Script
            while (iterator.NextVisible(false))
            {
                if (iterator.propertyPath == "weightMultipliers")
                {
                    DrawWeightMultipliersWithCurves(iterator);
                }
                else
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWeightMultipliersWithCurves(SerializedProperty prop)
        {
            prop.isExpanded = EditorGUILayout.Foldout(prop.isExpanded, prop.displayName, true);
            if (!prop.isExpanded) return;

            EditorGUI.indentLevel++;

            var child = prop.Copy();
            var end = prop.GetEndProperty();
            child.NextVisible(true); // enter children

            while (!SerializedProperty.EqualContents(child, end))
            {
                EditorGUILayout.PropertyField(child, true);

                if (child.name == "facingWidth")
                {
                    var width = ResolveEffectiveWidth(child.floatValue, true);
                    SettingsEditor.DrawFacingCurve("  Facing Cost Preview", width);
                }
                else if (child.name == "exposureWidth")
                {
                    var width = ResolveEffectiveWidth(child.floatValue, false);
                    SettingsEditor.DrawExposureCurve("  Exposure Cost Preview", width);
                }

                if (!child.NextVisible(false)) break;
            }

            EditorGUI.indentLevel--;
        }

        private float ResolveEffectiveWidth(float multiplier, bool isFacing)
        {
            var baseSettings = GetBaseSettings();
            var baseWidth = baseSettings
                ? (isFacing ? baseSettings.facingWidth : baseSettings.exposureWidth)
                : 0.5f;
            return baseWidth * multiplier;
        }

        private Settings GetBaseSettings()
        {
            if (baseSettingsResolved) return cachedBaseSettings;
            baseSettingsResolved = true;
            var guids = AssetDatabase.FindAssets("t:Movement.MPC.Settings");
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedBaseSettings = AssetDatabase.LoadAssetAtPath<Settings>(path);
            return cachedBaseSettings;
        }
    }
}
#endif
