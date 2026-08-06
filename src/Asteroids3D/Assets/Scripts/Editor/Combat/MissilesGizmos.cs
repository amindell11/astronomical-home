using System.Runtime.CompilerServices;
using Game.Diagnostics;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>Live-editor shim over <see cref="MissilesPainter"/>'s launcher view: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class MissilesGizmos
    {
        private static readonly ConditionalWeakTable<Missiles, Ship> ParentShips = new();

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Missiles))]
        private static void DrawAmmoLabel(Missiles missiles, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.Missiles, gizmoType)) return;
            var ship = ParentShips.GetValue(missiles, m => m.GetComponentInParent<Ship>());
            if (!ship) return;
            MissilesPainter.DrawLauncher(new GizmoCanvas(), missiles, ship.Kinematics.pos);
        }
    }
}
