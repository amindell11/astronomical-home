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
            if (!showGizmos) return;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
        
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
        
            var pos = transform.position;
            var targetPos = Target;
            Vector3 forward = getState!=null? getState().kinematics.Forward: Vector2.up;
            forward = new Vector3(forward.x, forward.y, 0f);
        
            // Line to target
            var distance = Vector3.Distance(pos, targetPos);
            Gizmos.color = Color.gray;
            
            Gizmos.DrawLine(pos, targetPos);
        
            // Target marker
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(targetPos, Vector3.one * 2f);
        
            // Angle to target display
            var angleToTarget = AngleToTarget;
        
            // Draw angle indicator
            Gizmos.color =  Color.red;
            var dirToTarget = (targetPos - pos).normalized;
            Gizmos.DrawRay(pos, dirToTarget * 5f);
        }
    
        void DrawLineOfSightGizmos()
        {
            if (!HasTarget || !primaryWeapon) return;

            var firePos = FirePoint;
            var targetPos = Target;
        
            // Line of sight ray
            var hasLOS = targetingUtils.HasLineOfSight(firePos, targetPos, AngleToTarget);
            Gizmos.color = hasLOS ? Color.green : Color.red;
            Gizmos.DrawLine(firePos, targetPos);
        
            // Fire point marker
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePos, 0.5f);
        
        }
    
        void DrawAngleCone(Vector3 origin, Vector3 forward, float angleInDegrees, float range)
        {
            var halfAngle = angleInDegrees * 0.5f;
        
            // Convert to 3D space (assuming 2D game on XY plane)
            var forward3D = forward.normalized;
        
            // Create left and right boundaries of the cone
            var leftRotation = Quaternion.AngleAxis(-halfAngle, Vector3.forward);
            var rightRotation = Quaternion.AngleAxis(halfAngle, Vector3.forward);
        
            var leftDirection = leftRotation * forward3D;
            var rightDirection = rightRotation * forward3D;
        
            // Draw cone edges
            Gizmos.DrawRay(origin, leftDirection * range);
            Gizmos.DrawRay(origin, rightDirection * range);
        
            // Draw arc at the end
            var segments = Mathf.Max(3, Mathf.RoundToInt(angleInDegrees / 5f));
            var prevPoint = origin + leftDirection * range;
        
            for (var i = 1; i <= segments; i++)
            {
                var t = (float)i / segments;
                var currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
                var rotation = Quaternion.AngleAxis(currentAngle, Vector3.forward);
                var direction = rotation * forward3D;
                var point = origin + direction * range;
            
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    }
}
#endif
