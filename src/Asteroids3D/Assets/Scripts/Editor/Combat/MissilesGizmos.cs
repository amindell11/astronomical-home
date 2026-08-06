using Game.Diagnostics;
using UnityEditor;

namespace Combat.Weapons
{
    /// <summary>Live-editor shim over <see cref="MissilesPainter"/>'s launcher view: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class MissilesGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Missiles))]
        private static void DrawAmmoLabel(Missiles missiles, GizmoType gizmoType)
        {
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.Missiles, gizmoType)) return;
            MissilesPainter.DrawLauncher(new GizmoCanvas(), missiles);
        }
    }
}
