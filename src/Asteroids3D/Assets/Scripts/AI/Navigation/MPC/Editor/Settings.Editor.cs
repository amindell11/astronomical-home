#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Movement.MPC
{
    [CustomEditor(typeof(MpcSettings))]
    public class SettingsEditor : Editor
    {
        // Fields after which to insert curve previews
        private static readonly string[] CurveAfter =
        {
            "terminalCurve",
            "facingWidth",
            "exposureWidth",
            "obstacleFalloffCurve",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var settings = (MpcSettings)target;

            var prop = serializedObject.GetIterator();
            prop.NextVisible(true); // skip m_Script

            while (prop.NextVisible(false))
            {
                EditorGUILayout.PropertyField(prop, true);

                switch (prop.name)
                {
                    case "positionCurve":
                        DrawPositionCurve(settings.positionCurve, settings.positionSaturationDistance);
                        break;
                    case "terminalCurve":
                        DrawTerminalRampCurve(settings.terminalMultiplier, settings.terminalCurve, settings.Horizon);
                        break;
                    case "facingWidth":
                        DrawFacingCurve(settings.facingWidth);
                        break;
                    case "exposureWidth":
                        DrawExposureCurve(settings.exposureWidth);
                        break;
                    case "obstacleFalloffCurve":
                        DrawObstacleFalloffCurve(settings.obstacleFalloffCurve);
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawPositionCurve(float curve, float satDistance)
        {
            var animCurve = new AnimationCurve();
            const int steps = 64;
            var maxDist = satDistance > 0f ? Mathf.Max(50f, satDistance * 2.5f) : 50f;
            var maxCost = satDistance > 0f ? 1f : Mathf.Pow(maxDist, curve);
            var satMax = satDistance > 0f ? Mathf.Pow(satDistance, curve) : 0f;

            for (var i = 0; i <= steps; i++)
            {
                var dist = maxDist * i / steps;
                var raw = Mathf.Pow(dist, curve);
                var cost = satDistance > 0f ? raw / (raw + satMax) : raw;
                animCurve.AddKey(new Keyframe(dist, cost) { weightedMode = WeightedMode.None });
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.CurveField(animCurve, Color.green,
                new Rect(0, 0, maxDist, maxCost),
                GUILayout.Height(50));
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawTerminalRampCurve(float multiplier, float curve, int horizon)
        {
            var animCurve = new AnimationCurve();
            var steps = Mathf.Max(horizon, 2);

            for (var i = 0; i < steps; i++)
            {
                var t = i / (float)(steps - 1);
                var ramp = Mathf.Pow(t, curve) * multiplier;
                animCurve.AddKey(new Keyframe(i, 1f + ramp) { weightedMode = WeightedMode.None });
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.CurveField(animCurve, Color.yellow,
                new Rect(0, 0, steps - 1, 1f + multiplier),
                GUILayout.Height(50));
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawObstacleFalloffCurve(float curve)
        {
            var animCurve = new AnimationCurve();
            const int steps = 64;
            const float epsSq = 0.0001f;
            var halfCurve = curve * 0.5f;

            var refCost = 1f / Mathf.Pow(0.25f + epsSq, halfCurve);

            for (var i = 0; i <= steps; i++)
            {
                var norm = (float)i / steps;
                var normSq = norm * norm;
                var cost = 1f / Mathf.Pow(normSq + epsSq, halfCurve);
                animCurve.AddKey(new Keyframe(norm, cost / refCost) { weightedMode = WeightedMode.None });
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.CurveField(animCurve, Color.red,
                new Rect(0, 0, 1f, 5f),
                GUILayout.Height(50));
            EditorGUI.EndDisabledGroup();
        }

        internal static void DrawFacingCurve(float width)
        {
            var curve = new AnimationCurve();
            const int steps = 64;
            var maxInput = Mathf.PI;
            var w = Mathf.Max(width, 1e-4f);

            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var input = -maxInput + t * 2f * maxInput;
                var err = Mathf.Abs(input);
                var cost = err < w ? err * err : 2f * w * err - w * w;
                curve.AddKey(new Keyframe(input, cost) { weightedMode = WeightedMode.None });
            }

            var maxCost = 2f * w * maxInput - w * w;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.CurveField(curve, Color.cyan,
                new Rect(-maxInput, 0, 2f * maxInput, maxCost),
                GUILayout.Height(50));
            EditorGUI.EndDisabledGroup();
        }

        internal static void DrawExposureCurve(float width)
        {
            var curve = new AnimationCurve();
            const int steps = 64;
            var maxAngle = Mathf.PI;
            var w = Mathf.Max(width, 1e-4f);

            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var angle = -maxAngle + t * 2f * maxAngle;
                var x = angle / w;
                var cost = Mathf.Exp(-x * x);
                curve.AddKey(new Keyframe(angle, cost) { weightedMode = WeightedMode.None });
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.CurveField(curve, Color.cyan,
                new Rect(-maxAngle, 0, 2f * maxAngle, 1f),
                GUILayout.Height(50));
            EditorGUI.EndDisabledGroup();
        }

    }
}
#endif
