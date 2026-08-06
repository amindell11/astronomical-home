using System.Runtime.CompilerServices;
using Game.Diagnostics;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Ships.Damage
{
    /// <summary>Live-editor shim over <see cref="DamageBarsPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class DamageControllerGizmos
    {
        private static readonly ConditionalWeakTable<DamageController, Ship> ParentShips = new();

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(DamageController))]
        private static void DrawHealthBars(DamageController damage, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.DamageBars, gizmoType)) return;
            var ship = ParentShips.GetValue(damage, d => d.GetComponentInParent<Ship>());
            if (!ship) return;
            DamageBarsPainter.Draw(new GizmoCanvas(), damage, ship.Kinematics.pos);
        }
    }
}
