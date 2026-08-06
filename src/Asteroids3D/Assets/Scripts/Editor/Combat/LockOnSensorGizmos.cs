using Combat.Conditions;
using Game;
using Game.Diagnostics;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Combat.Targeting
{
    /// <summary>Live-editor shim over <see cref="LockOnPainter"/>: the DrawGizmo per-subject hook plus DiagnosticGate gating, rendering the shared painter onto a <see cref="GizmoCanvas"/>.</summary>
    internal static class LockOnSensorGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(LockOnSensor))]
        private static void Draw(LockOnSensor sensor, GizmoType gizmoType)
        {
            if (!DiagnosticGate.ShouldDraw(DiagnosticPainters.LockOn, gizmoType)) return;
            // Edit mode: Awake hasn't cached the ship and Kinematics is unpopulated — derive both from transforms.
            var ship = sensor.selfShip ? sensor.selfShip : sensor.GetComponentInParent<Ship>();
            if (!ship) return;
            var subject = Application.isPlaying
                ? ship.Kinematics.pos
                : GamePlane.WorldPointToPlane(ship.transform.position);
            var cooldown = sensor.weapon ? sensor.weapon.GetComponent<Cooldown>() : null;
            LockOnPainter.Draw(new GizmoCanvas(), sensor, cooldown, subject);
        }
    }
}
