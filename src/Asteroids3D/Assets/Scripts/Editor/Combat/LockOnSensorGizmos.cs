using Combat.Conditions;
using Game;
using Game.Diagnostics;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Combat.Targeting
{
    /// <summary>Lock-on sensor state in plane space: sensor-cone ray fan, max-range ring, forward ray, lock line + target ring, lock-progress arc, and a state/lock/cooldown readout — all colored by <see cref="LockState"/>.</summary>
    internal static class LockOnSensorGizmos
    {
        private const float ConeRayStepDeg = 5f;
        private const float TargetRingRadius = 1f;
        private const float ProgressArcRadius = 2f;

        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        [DrawGizmo(GizmoType.Selected, typeof(LockOnSensor))]
        private static void Draw(LockOnSensor sensor, GizmoType gizmoType)
        {
            if (!sensor.firePoint) return;
            // Edit mode: Awake hasn't cached the ship and Kinematics is unpopulated — derive both from transforms.
            var ship = sensor.selfShip ? sensor.selfShip : sensor.GetComponentInParent<Ship>();
            if (!ship) return;

            var origin = GamePlane.WorldPointToPlane(sensor.firePoint.position);
            var forward = SafeDir(GamePlane.WorldDirToPlane(sensor.firePoint.up));
            var stateColor = StateColor(sensor.State);

            DrawCone(sensor, origin, forward, stateColor);
            Ring(origin, sensor.maxLockDistance, new Color(stateColor.r, stateColor.g, stateColor.b, 0.3f));
            Line(origin, origin + forward * sensor.maxLockDistance, stateColor);
            DrawTarget(sensor, origin, stateColor);
            if (sensor.State == LockState.Locking) DrawProgressArc(origin, sensor.LockProgress);

            var cooldown = sensor.weapon ? sensor.weapon.GetComponent<Cooldown>() : null;
            var cooldownRemaining = cooldown ? cooldown.CooldownRemaining : 0f;
            var subject = Application.isPlaying
                ? ship.Kinematics.pos
                : GamePlane.WorldPointToPlane(ship.transform.position);
            ShipReadout.Draw(subject, ShipReadoutRow.LockOn,
                $"Targeting: {sensor.State}\nLock: {sensor.LockProgress:P0}\nCooldown: {cooldownRemaining:F1}s",
                stateColor);
        }

        private static void DrawCone(LockOnSensor sensor, Vector2 origin, Vector2 forward, Color color)
        {
            var halfAngle = sensor.lockOnConeAngle / 2f;
            var faint = new Color(color.r, color.g, color.b, 0.1f);
            var range = sensor.maxLockDistance;

            Line(origin, origin + Rotate(forward, -halfAngle) * range, faint);
            Line(origin, origin + Rotate(forward, halfAngle) * range, faint);

            var raysPerSide = Mathf.FloorToInt(halfAngle / ConeRayStepDeg);
            for (var i = 1; i <= raysPerSide; i++)
            {
                var angle = i * ConeRayStepDeg;
                Line(origin, origin + Rotate(forward, -angle) * range, faint);
                Line(origin, origin + Rotate(forward, angle) * range, faint);
            }
        }

        private static void DrawTarget(LockOnSensor sensor, Vector2 origin, Color stateColor)
        {
            var target = sensor.CurrentTarget;
            if (target == null || !target.TargetPoint) return;
            var targetPos = GamePlane.WorldPointToPlane(target.TargetPoint.position);
            Line(origin, targetPos, stateColor);
            Ring(targetPos, TargetRingRadius, sensor.State == LockState.Locked ? Color.green : Color.red);
        }

        private static void DrawProgressArc(Vector2 center, float progress)
        {
            Handles.color = Color.Lerp(Color.red, Color.green, progress);
            Handles.DrawWireArc(GamePlane.PlanePointToWorld(center), PlaneNormal,
                GamePlane.PlaneDirToWorld(Vector2.right), progress * 360f, ProgressArcRadius);
        }

        private static void Ring(Vector2 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius);
        }

        private static void Line(Vector2 a, Vector2 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
        }

        private static Color StateColor(LockState state) => state switch
        {
            LockState.Idle => Color.white,
            LockState.Locking => Color.yellow,
            LockState.Locked => Color.green,
            _ => Color.gray,
        };

        private static Vector2 SafeDir(Vector2 v) => v.sqrMagnitude > 1e-8f ? v.normalized : Vector2.up;

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
