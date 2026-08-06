using System.Runtime.CompilerServices;
using Combat.Conditions;
using Game.Diagnostics;
using Ships;
using UnityEditor;

namespace Combat.Targeting
{
    /// <summary>Live-editor shim over <see cref="LockOnPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class LockOnSensorGizmos
    {
        private static readonly ConditionalWeakTable<LockOnSensor, Ship> Ships = new();

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(LockOnSensor))]
        private static void Draw(LockOnSensor sensor, GizmoType gizmoType)
        {
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.LockOn, gizmoType)) return;
            var ship = Ships.GetValue(sensor, s => s.GetComponentInParent<Ship>());
            if (!ship) return;
            var cooldown = sensor.weapon ? sensor.weapon.GetComponent<Cooldown>() : null;
            LockOnPainter.Draw(new GizmoCanvas(), sensor, cooldown, ship.Kinematics.pos);
        }
    }
}
