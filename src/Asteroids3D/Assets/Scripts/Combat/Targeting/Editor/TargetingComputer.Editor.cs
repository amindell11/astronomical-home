#if UNITY_EDITOR
using Combat.Conditions;
using UnityEditor;
using UnityEngine;

namespace Combat.Targeting
{
    public partial class TargetingComputer
    {
        void OnDrawGizmos()
        {
            if (firePoint == null) return;
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

            if (State == LockState.Locking && lockOnTime > 0f)
            {
                var progress = Mathf.Clamp01(lockTimer / lockOnTime);
                Gizmos.color = Color.Lerp(Color.red, Color.green, progress);
            
                var segments = 16;
                var radius = 2f;
                for (var i = 0; i < segments * progress; i++)
                {
                    var angle1 = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                    var angle2 = ((i + 1) / (float)segments) * 360f * Mathf.Deg2Rad;
                
                    var p1 = origin + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * radius;
                    var p2 = origin + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * radius;
                
                    Gizmos.DrawLine(p1, p2);
                }
            }
            
            var cooldownRemaining = 0f;
            if (weapon != null)
            {
                var cooldown = weapon.GetComponent<Cooldown>();
                if (cooldown != null)
                {
                    cooldownRemaining = cooldown.CooldownRemaining;
                }
            }

            Handles.color = stateColor;
            Handles.Label(origin + Vector3.up * 3f, $"Targeting: {State}\nTimer: {lockTimer:F1}s\nCooldown: {cooldownRemaining:F1}s");
        }
    }
}
#endif
