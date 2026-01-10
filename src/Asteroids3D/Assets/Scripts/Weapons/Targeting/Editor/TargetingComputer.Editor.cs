#if UNITY_EDITOR
using Ships.Weapons.Conditions;
using UnityEditor;
using UnityEngine;

namespace Weapons
{
    public partial class TargetingComputer
    {
        void OnDrawGizmos()
        {
            if (firePoint == null) return;
            Vector3 origin = firePoint.position;
            Vector3 forward = firePoint.up;

            Color stateColor = State switch
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
                Vector3 targetPos = CurrentTarget.TargetPoint.position;
            
                Gizmos.color = stateColor;
                Gizmos.DrawLine(origin, targetPos);
            
                Gizmos.color = State == LockState.Locked ? Color.green : Color.red;
                Gizmos.DrawWireSphere(targetPos, 1f);
            }

            if (State == LockState.Locking && lockOnTime > 0f)
            {
                float progress = Mathf.Clamp01(lockTimer / lockOnTime);
                Gizmos.color = Color.Lerp(Color.red, Color.green, progress);
            
                int segments = 16;
                float radius = 2f;
                for (int i = 0; i < segments * progress; i++)
                {
                    float angle1 = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                    float angle2 = ((i + 1) / (float)segments) * 360f * Mathf.Deg2Rad;
                
                    Vector3 p1 = origin + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * radius;
                    Vector3 p2 = origin + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * radius;
                
                    Gizmos.DrawLine(p1, p2);
                }
            }
            
            float cooldownRemaining = 0f;
            if (launcher != null)
            {
                var cooldown = launcher.GetComponent<Cooldown>();
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
