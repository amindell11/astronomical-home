#if UNITY_EDITOR
using Game;
using UnityEngine;

namespace AI
{
    public partial class Gunner
    {
        [Header("Debug Gizmos")]
        [SerializeField]
        private bool showGizmos = true;
        [SerializeField] private bool showTargeting = true;
        [SerializeField] private bool showLineOfSight = true;

        private void OnDrawGizmos()
        {
            if (!showGizmos || !ship) return;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || !ship) return;
        
            if (showTargeting)
            {
                DrawTargetingGizmos();
            }
        
            if (showLineOfSight)
            {
                DrawLineOfSightGizmos();
            }
        }
        void DrawTargetingGizmos()
        {
            if (!HasTarget) return;
        
            Vector3 pos = transform.position;
            Vector3 targetPos = Target;
            Vector3 forward = ship?.CurrentState.Kinematics.Forward ?? Vector2.up;
            forward = new Vector3(forward.x, forward.y, 0f);
        
            // Line to target
            float distance = Vector3.Distance(pos, targetPos);
            Gizmos.color = Color.gray;
            
            Gizmos.DrawLine(pos, targetPos);
        
            // Target marker
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(targetPos, Vector3.one * 2f);
        
            // Angle to target display
            float angleToTarget = AngleToTarget;
        
            // Draw angle indicator
            Gizmos.color =  Color.red;
            Vector3 dirToTarget = (targetPos - pos).normalized;
            Gizmos.DrawRay(pos, dirToTarget * 5f);
        }
    
        void DrawLineOfSightGizmos()
        {
            if (!HasTarget || !ship.Weapons.Primary) return;

            Vector3 firePos = FirePoint;
            Vector3 targetPos = Target;
        
            // Line of sight ray
            bool hasLOS = targeting.HasLineOfSight(firePos, targetPos, AngleToTarget);
            Gizmos.color = hasLOS ? Color.green : Color.red;
            Gizmos.DrawLine(firePos, targetPos);
        
            // Fire point marker
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePos, 0.5f);
        
        }
    
        void DrawAngleCone(Vector3 origin, Vector3 forward, float angleInDegrees, float range)
        {
            float halfAngle = angleInDegrees * 0.5f;
        
            // Convert to 3D space (assuming 2D game on XY plane)
            Vector3 forward3D = forward.normalized;
        
            // Create left and right boundaries of the cone
            Quaternion leftRotation = Quaternion.AngleAxis(-halfAngle, Vector3.forward);
            Quaternion rightRotation = Quaternion.AngleAxis(halfAngle, Vector3.forward);
        
            Vector3 leftDirection = leftRotation * forward3D;
            Vector3 rightDirection = rightRotation * forward3D;
        
            // Draw cone edges
            Gizmos.DrawRay(origin, leftDirection * range);
            Gizmos.DrawRay(origin, rightDirection * range);
        
            // Draw arc at the end
            int segments = Mathf.Max(3, Mathf.RoundToInt(angleInDegrees / 5f));
            Vector3 prevPoint = origin + leftDirection * range;
        
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Quaternion rotation = Quaternion.AngleAxis(currentAngle, Vector3.forward);
                Vector3 direction = rotation * forward3D;
                Vector3 point = origin + direction * range;
            
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    }
}
#endif
