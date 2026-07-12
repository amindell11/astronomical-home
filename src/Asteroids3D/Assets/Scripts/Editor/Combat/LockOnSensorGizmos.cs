using Combat.Conditions;
using UnityEditor;
using UnityEngine;

namespace Combat.Targeting
{
    internal static class LockOnSensorGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(LockOnSensor))]
        private static void Draw(LockOnSensor sensor, GizmoType gizmoType)
        {
            if (!sensor.firePoint) return;
            var origin = sensor.firePoint.position;
            var forward = sensor.firePoint.up;

            var stateColor = sensor.State switch
            {
                LockState.Idle => Color.white,
                LockState.Locking => Color.yellow,
                LockState.Locked => Color.green,
                LockState.Cooldown => Color.gray,
                _ => Color.gray
            };

            DrawSensorCone(sensor, origin, forward, stateColor);

            Gizmos.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.3f);
            Gizmos.DrawWireSphere(origin, sensor.maxLockDistance);

            Gizmos.color = stateColor;
            Gizmos.DrawRay(origin, forward * sensor.maxLockDistance);

            if (sensor.CurrentTarget != null && sensor.CurrentTarget.TargetPoint != null)
            {
                var targetPos = sensor.CurrentTarget.TargetPoint.position;

                Gizmos.color = stateColor;
                Gizmos.DrawLine(origin, targetPos);

                Gizmos.color = sensor.State == LockState.Locked ? Color.green : Color.red;
                Gizmos.DrawWireSphere(targetPos, 1f);
            }

            if (sensor.State == LockState.Locking)
            {
                var progress = sensor.LockProgress;
                Gizmos.color = Color.Lerp(Color.red, Color.green, progress);

                const int segments = 16;
                const float radius = 2f;
                for (var i = 0; i < segments * progress; i++)
                {
                    var angle1 = i / (float)segments * 360f * Mathf.Deg2Rad;
                    var angle2 = ((i + 1) / (float)segments) * 360f * Mathf.Deg2Rad;

                    var p1 = origin + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * radius;
                    var p2 = origin + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * radius;

                    Gizmos.DrawLine(p1, p2);
                }
            }

            var cooldownRemaining = 0f;
            if (sensor.weapon)
            {
                var cooldown = sensor.weapon.GetComponent<Cooldown>();
                if (cooldown)
                    cooldownRemaining = cooldown.CooldownRemaining;
            }

            Handles.color = stateColor;
            Handles.Label(origin + Vector3.up * 3f,
                $"Targeting: {sensor.State}\nLock: {sensor.LockProgress:P0}\nCooldown: {cooldownRemaining:F1}s");
        }

        private static void DrawSensorCone(LockOnSensor sensor, Vector3 origin, Vector3 forward, Color color)
        {
            var halfAngle = sensor.lockOnConeAngle / 2f;
            var planeNormal = Game.GamePlane.Normal;

            Gizmos.color = new Color(color.r, color.g, color.b, 0.1f);

            var leftDir = Quaternion.AngleAxis(-halfAngle, planeNormal) * forward;
            var rightDir = Quaternion.AngleAxis(halfAngle, planeNormal) * forward;

            Gizmos.DrawRay(origin, leftDir * sensor.maxLockDistance);
            Gizmos.DrawRay(origin, rightDir * sensor.maxLockDistance);

            const float degreesBetweenRays = 5f;
            var raysPerSide = Mathf.FloorToInt(halfAngle / degreesBetweenRays);

            for (var i = 1; i <= raysPerSide; i++)
            {
                var angle = i * degreesBetweenRays;
                var leftRay = Quaternion.AngleAxis(-angle, planeNormal) * forward;
                var rightRay = Quaternion.AngleAxis(angle, planeNormal) * forward;

                Gizmos.DrawRay(origin, leftRay * sensor.maxLockDistance);
                Gizmos.DrawRay(origin, rightRay * sensor.maxLockDistance);
            }
        }
    }
}
