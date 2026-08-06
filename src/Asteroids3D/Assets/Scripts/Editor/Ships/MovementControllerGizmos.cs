using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Ships.Movement
{
    /// <summary>Live-editor shim over <see cref="MovementForcesPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class MovementControllerGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(MovementController))]
        private static void DrawForces(MovementController mover, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.MovementForces, gizmoType)) return;
            MovementForcesPainter.Draw(new GizmoCanvas(), mover);
        }
    }
}
