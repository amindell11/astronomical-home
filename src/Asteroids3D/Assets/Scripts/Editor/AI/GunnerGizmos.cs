using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Live-editor shim over <see cref="GunnerTargetingPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class GunnerGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Gunner))]
        private static void Draw(Gunner gunner, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.GunnerTargeting, gizmoType)) return;
            GunnerTargetingPainter.Draw(new GizmoCanvas(), gunner);
        }
    }
}
