#if UNITY_EDITOR
using Combat.Conditions;
using UnityEditor;
using UnityEngine;

namespace Combat.Targeting
{
    public partial class LockOnSensor
    {
        private void OnDrawGizmos()
        {
            if (!firePoint) return;
            var origin = firePoint.position;
            var forward = firePoint.up;

            var stateColor = State switch
            {
                LockState.Idle => Color.white,
                LockState.Locking => Color.yellow,
                LockState.Locked => Color.green,
                LockState.Cooldown => Color.gray,
                _ => Color.gray
            };

            DrawSensorCone(origin, forward, stateColor);

            Gizmos.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.3f);
            Gizmos.DrawWireSphere(origin, maxLockDistance);

            Gizmos.color = stateColor;
            Gizmos.DrawRay(origin, forward * maxLockDistance);

            if (CurrentTarget != null && CurrentTarget.TargetPoint != null)
            {
                var targetPos = CurrentTarget.TargetPoint.position;
            
                Gizmos.color = stateColor;
                Gizmos.DrawLine(origin, targetPos);
            
                Gizmos.color = State == LockState.Locked ? Color.green : Color.red;
                Gizmos.DrawWireSphere(targetPos, 1f);
            }

            if (State == LockState.Locking)
            {
                var progress = LockProgress;
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
            if (weapon)
            {
                var cooldown = weapon.GetComponent<Cooldown>();
                if (cooldown)
                {
                    cooldownRemaining = cooldown.CooldownRemaining;
                }
            }

            Handles.color = stateColor;
            Handles.Label(origin + Vector3.up * 3f, $"Targeting: {State}\nLock: {LockProgress:P0}\nCooldown: {cooldownRemaining:F1}s");
        }

        private void DrawSensorCone(Vector3 origin, Vector3 forward, Color color)
        {
            var halfAngle = lockOnConeAngle / 2f;
            var planeNormal = Game.GamePlane.Normal;

            Gizmos.color = new Color(color.r, color.g, color.b, 0.1f);

            var leftDir = Quaternion.AngleAxis(-halfAngle, planeNormal) * forward;
            var rightDir = Quaternion.AngleAxis(halfAngle, planeNormal) * forward;

            Gizmos.DrawRay(origin, leftDir * maxLockDistance);
            Gizmos.DrawRay(origin, rightDir * maxLockDistance);

            const float degreesBetweenRays = 5f;
            var raysPerSide = Mathf.FloorToInt(halfAngle / degreesBetweenRays);

            for (var i = 1; i <= raysPerSide; i++)
            {
                var angle = i * degreesBetweenRays;
                var leftRay = Quaternion.AngleAxis(-angle, planeNormal) * forward;
                var rightRay = Quaternion.AngleAxis(angle, planeNormal) * forward;

                Gizmos.DrawRay(origin, leftRay * maxLockDistance);
                Gizmos.DrawRay(origin, rightRay * maxLockDistance);
            }
        }
    }
}
#endif
