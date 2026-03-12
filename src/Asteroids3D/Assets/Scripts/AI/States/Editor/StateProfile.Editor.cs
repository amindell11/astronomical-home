#if UNITY_EDITOR
using Movement.MPC;
using UnityEditor;
using UnityEngine;

namespace AI.States
{
    [CustomEditor(typeof(StateProfile))]
    public class StateProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var profile = (StateProfile)target;
            var mults = profile.weightMultipliers;

            // Find the base MpcSettings to resolve effective values
            var baseSettings = FindBaseSettings();
            var baseFacingWidth = baseSettings ? baseSettings.facingWidth : 0.5f;
            var baseExposureWidth = baseSettings ? baseSettings.exposureWidth : 0.5f;

            var effectiveFacing = mults.facingWidth > 0f ? mults.facingWidth : baseFacingWidth;
            var effectiveExposure = mults.exposureWidth > 0f ? mults.exposureWidth : baseExposureWidth;

            EditorGUILayout.Space();
            var suffix = mults.facingWidth > 0f || mults.exposureWidth > 0f ? " (with overrides)" : " (base values)";
            EditorGUILayout.LabelField("Cost Curve Previews" + suffix, EditorStyles.boldLabel);

            SettingsEditor.DrawFacingCurve("Facing Cost", effectiveFacing);
            SettingsEditor.DrawExposureCurve("Exposure Cost", effectiveExposure);
        }

        private static Settings FindBaseSettings()
        {
            var guids = AssetDatabase.FindAssets("t:Movement.MPC.Settings");
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Settings>(path);
        }
    }
}
#endif
