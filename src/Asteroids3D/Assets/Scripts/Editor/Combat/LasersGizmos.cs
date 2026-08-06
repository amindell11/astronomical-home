using System.Runtime.CompilerServices;
using Game.Diagnostics;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>Live-editor shim over <see cref="LaserHeatPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class LasersGizmos
    {
        private static readonly ConditionalWeakTable<Lasers, Ship> ParentShips = new();

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Lasers))]
        private static void DrawHeatBar(Lasers lasers, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.LaserHeat, gizmoType)) return;
            var ship = ParentShips.GetValue(lasers, l => l.GetComponentInParent<Ship>());
            if (!ship) return;
            LaserHeatPainter.Draw(new GizmoCanvas(), lasers, ship.Kinematics.pos);
        }
    }
}
