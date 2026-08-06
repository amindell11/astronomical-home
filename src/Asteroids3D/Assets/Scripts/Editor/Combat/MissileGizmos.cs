using Game.Diagnostics;
using UnityEditor;

namespace Combat.Projectile
{
    /// <summary>Live-editor shim over <see cref="MissilesPainter"/>'s per-missile view: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class MissileGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Missile))]
        private static void Draw(Missile missile, GizmoType gizmoType)
        {
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.Missiles, gizmoType)) return;
            MissilesPainter.Draw(new GizmoCanvas(), missile);
        }
    }
}
