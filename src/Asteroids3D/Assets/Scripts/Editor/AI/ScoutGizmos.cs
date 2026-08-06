using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Live-editor shim over <see cref="ScoutPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class ScoutGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Scout))]
        private static void Draw(Scout scout, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.ScoutScan, gizmoType)) return;
            ScoutPainter.Draw(new GizmoCanvas(), scout);
        }
    }
}
