using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>Live-editor shim over <see cref="LaserHeatPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class LasersGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Lasers))]
        private static void DrawHeatBar(Lasers lasers, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.LaserHeat, gizmoType)) return;
            LaserHeatPainter.Draw(new GizmoCanvas(), lasers);
        }
    }
}
