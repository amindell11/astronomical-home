using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Ships.Damage
{
    /// <summary>Live-editor shim over <see cref="DamageBarsPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class DamageControllerGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(DamageController))]
        private static void DrawHealthBars(DamageController damage, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.DamageBars, gizmoType)) return;
            DamageBarsPainter.Draw(new GizmoCanvas(), damage);
        }
    }
}
