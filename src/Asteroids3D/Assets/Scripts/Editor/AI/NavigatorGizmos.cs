using Game.Diagnostics;
using Movement.MPC;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Live-editor shim over the navigator painters, plus the control-input bar panel. The panel is camera-facing billboard UI with no plane-space form, so it stays an editor-only draw under its own <see cref="MpcControls"/> atom and never reaches a capture.</summary>
    internal static class NavigatorGizmos
    {
        internal const string MpcControls = "mpc-controls";

        private static readonly Vector3 ControlPanelOffset = new(0f, 2.5f, 0f);
        private const float BarWidth = 1.2f;
        private const float BarHeight = 0.12f;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Navigator))]
        private static void Draw(Navigator nav, GizmoType gizmoType)
        {
            if (!Application.isPlaying || nav.mpc == null) return;
            var canvas = new GizmoCanvas();

            if (DiagnosticGate.ShouldDraw(DiagnosticPainters.MpcTrajectories, gizmoType))
                NavigatorTrajectoryPainter.Draw(canvas, nav);
            if (DiagnosticGate.ShouldDraw(DiagnosticPainters.MpcObstacles, gizmoType))
                NavigatorObstaclePainter.Draw(canvas, nav);
            if (DiagnosticGate.ShouldDraw(MpcControls, gizmoType))
                DrawControlInputs(nav);
        }

        private static void DrawControlInputs(Navigator nav)
        {
            var sequence = nav.bestSequence;
            if (sequence == null || sequence.Length == 0) return;

            var cam = Camera.current;
            if (cam == null) return;

            var raw = sequence[0];
            var origin = nav.transform.position + ControlPanelOffset;
            var right = cam.transform.right;
            var up = cam.transform.up;

            var labelStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleRight,
            };
            var valueStyle = new GUIStyle
            {
                fontSize = 10,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                alignment = TextAnchor.MiddleLeft,
            };

            DrawControlBar(origin, right, up, 0, "THR", raw.thrust,
                new Color(0.2f, 0.9f, 0.3f), labelStyle, valueStyle);
            DrawControlBar(origin, right, up, 1, "STR", raw.strafe,
                new Color(0.3f, 0.6f, 1f), labelStyle, valueStyle);
            DrawControlBar(origin, right, up, 2, "YAW", raw.yawTorque,
                new Color(1f, 0.4f, 0.8f), labelStyle, valueStyle);
        }

        private static void DrawControlBar(Vector3 origin, Vector3 right, Vector3 up,
            int row, string label, float value, Color color, GUIStyle labelStyle, GUIStyle valueStyle)
        {
            var halfBar = BarWidth * 0.5f;
            var center = origin - up * (row * 0.22f);
            var barLeft = center - right * halfBar;

            DrawQuad(barLeft, right, up, BarWidth, BarHeight, new Color(0.15f, 0.15f, 0.15f, 0.6f));

            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            Gizmos.DrawLine(center - up * BarHeight * 0.5f, center + up * BarHeight * 0.5f);

            // Applied control bar — center of the bar is the zero point.
            var barColor = color;
            barColor.a = 0.85f;
            var valueBarWidth = Mathf.Abs(value) * halfBar;
            var valueBarOrigin = value >= 0 ? center : center - right * valueBarWidth;
            DrawQuad(valueBarOrigin, right, up, valueBarWidth, BarHeight * 0.9f, barColor);

            Handles.Label(barLeft - right * 0.05f, label, labelStyle);
            Handles.Label(barLeft + right * (BarWidth + 0.08f), $"{value:+0.00;-0.00}", valueStyle);
        }

        private static void DrawQuad(Vector3 bottomLeft, Vector3 right, Vector3 up,
            float width, float height, Color color)
        {
            if (width < 0.001f) return;
            Gizmos.color = color;
            // Gizmos has no filled quad; approximate with horizontal scan lines.
            var steps = Mathf.Max(2, Mathf.CeilToInt(height / 0.02f));
            for (var i = 0; i <= steps; i++)
            {
                var y = up * (i / (float)steps * height - height * 0.5f);
                Gizmos.DrawLine(bottomLeft + y, bottomLeft + right * width + y);
            }
        }
    }
}
