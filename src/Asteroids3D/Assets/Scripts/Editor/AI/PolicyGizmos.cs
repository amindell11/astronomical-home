using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Live-editor shim over <see cref="PolicyPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class PolicyGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.Policy, gizmoType)) return;
            PolicyPainter.Draw(new GizmoCanvas(), commander);
        }
    }
}
