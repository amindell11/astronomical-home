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

                    if (iterator.name == "goal")
                    {
                        var profile = (StateProfile)target;
                        if (profile.goal is TrackEnemyGoal track)
                            DrawRangeBandCurve(track.desiredRange, track.rangeTolerance);
                    }
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
                    SettingsEditor.DrawFacingCurve(width);
                }
                else if (child.name == "exposureWidth")
                {
                    var width = ResolveEffectiveWidth(child.floatValue, false);
                    SettingsEditor.DrawExposureCurve(width);
                }

                if (!child.NextVisible(false)) break;
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawRangeBandCurve(float desiredRange, float tolerance)
        {
            var inner = desiredRange - tolerance;
            var outer = desiredRange + tolerance;
            var maxDist = outer * 2.5f;
            var curve = new AnimationCurve();
            const int steps = 80;

            for (var i = 0; i <= steps; i++)
            {
                var dist = maxDist * i / steps;
                float cost;
                if (dist >= inner && dist <= outer)
                    cost = 0f;
                else if (dist > outer)
                {
                    var err = dist - outer;
                    cost = err * err;
                }
                else
                {
                    var t = dist / Mathf.Max(inner, 1e-4f);
                    cost = -(1f - t * t);
                }
                curve.AddKey(new Keyframe(dist, cost) { weightedMode = WeightedMode.None });
            }

            // Cap display at 5x the reward magnitude so both sides are visible
            var displayMin = -1.5f;
            var displayMax = 5f;

            EditorGUILayout.LabelField($"  Range Band  —  reward ← [{inner:F0}  ...  {outer:F0}] → penalty");
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.CurveField(curve, Color.cyan,
                new Rect(0, displayMin, maxDist, displayMax - displayMin),
                GUILayout.Height(60));
            EditorGUI.EndDisabledGroup();
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
