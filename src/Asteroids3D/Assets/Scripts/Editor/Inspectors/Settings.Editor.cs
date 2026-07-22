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
                    case "terminalCurve":
                        DrawTerminalRampCurve(settings.terminalMultiplier, settings.terminalCurve, settings.Horizon);
                        break;
                    case "facingWidth":
                        DrawFacingCurve(settings.facingWidth);
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
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

    }
}
