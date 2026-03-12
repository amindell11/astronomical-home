#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Movement.MPC
{
    [CustomEditor(typeof(Settings))]
    public class SettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var settings = (Settings)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cost Curve Previews", EditorStyles.boldLabel);

            DrawFacingCurve("Facing Cost", settings.facingWidth);
            DrawExposureCurve("Exposure Cost", settings.exposureWidth);
            DrawRelaxCurve("Relaxation", settings.relaxMin, settings.relaxMax, settings.relaxCurve);
        }

        /// <summary>
        /// Facing cost: Huber loss over [-π, π], symmetric around 0.
        /// Quadratic within ±width, linear beyond.
        /// </summary>
        internal static void DrawFacingCurve(string label, float width)
        {
            EditorGUILayout.LabelField($"{label}  —  Huber(err, width={width:F2})");

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
                GUILayout.Height(60));
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Exposure cost: exp(-(angle/width)²) over angle [-π, π].
        /// Bell curve centered on enemy's nose (angle=0).
        /// </summary>
        internal static void DrawExposureCurve(string label, float width)
        {
            EditorGUILayout.LabelField($"{label}  —  exp(-(angle/{width:F2})²)");

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
                GUILayout.Height(60));
            EditorGUI.EndDisabledGroup();
        }
        /// <summary>
        /// Relaxation ramp: urgency = pow((cost - min) / (max - min), curve), clamped [0, 1].
        /// </summary>
        internal static void DrawRelaxCurve(string label, float min, float max, float curve)
        {
            EditorGUILayout.LabelField($"{label}  —  pow((cost - {min:F2}) / ({max:F2} - {min:F2}), {curve:F2})");

            var animCurve = new AnimationCurve();
            const int steps = 64;
            var range = Mathf.Max(max - min, 1e-4f);
            // Show a bit beyond relaxMax so the plateau is visible
            var maxCost = max * 1.25f;

            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var cost = t * maxCost;
                var normalized = Mathf.Clamp01((cost - min) / range);
                var urgency = Mathf.Pow(normalized, curve);
                animCurve.AddKey(new Keyframe(cost, urgency) { weightedMode = WeightedMode.None });
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.CurveField(animCurve, Color.green,
                new Rect(0, 0, maxCost, 1f),
                GUILayout.Height(60));
            EditorGUI.EndDisabledGroup();
        }
    }
}
#endif
