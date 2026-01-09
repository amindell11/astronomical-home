#if UNITY_EDITOR
using Game;
using UnityEngine;

namespace EnemyAI
{
    public partial class AIGunner
    {
        void OnDrawGizmos()
        {
            if (!showGizmos || !ship) return;
        
            if (showRanges)
            {
                DrawRangeGizmos();
            }
        }
    
        void OnDrawGizmosSelected()
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
    
        void DrawRangeGizmos()
        {
            Vector3 pos = transform.position;
        
            // Laser firing range
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, fireDistance);
        
            // Missile range
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, missileRange);
        
            // Optimal laser range (minimum)
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(pos, 3f);
        }
    
        void DrawTargetingGizmos()
        {
            if (Target == Vector2.zero) return;
        
            Vector3 pos = transform.position;
            Vector3 targetPos = GamePlane.PlanePointToWorld(Target);
            Vector3 forward = ship?.CurrentState.Kinematics.Forward ?? Vector2.up;
            forward = new Vector3(forward.x, forward.y, 0f);
        
            // Line to target
            float distance = Vector3.Distance(pos, targetPos);
            bool inLaserRange = distance <= fireDistance;
            bool inMissileRange = distance <= missileRange;
        
            if (inLaserRange)
                Gizmos.color = Color.red;
            else if (inMissileRange)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.gray;
            
            Gizmos.DrawLine(pos, targetPos);
        
            // Target marker
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(targetPos, Vector3.one * 2f);
        
            // Fire angle tolerance cone for laser
            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            DrawAngleCone(pos, forward, fireAngleTolerance, fireDistance);
        
            // Fire angle tolerance cone for missile
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            DrawAngleCone(pos, forward, missileAngleTolerance, missileRange);
        
            // Angle to target display
            float angleToTarget = AngleToTarget;
            bool laserCanFire = angleToTarget <= fireAngleTolerance && inLaserRange;
            bool missileCanFire = angleToTarget <= missileAngleTolerance && inMissileRange;
        
            // Draw angle indicator
            Gizmos.color = laserCanFire ? Color.green : (missileCanFire ? Color.yellow : Color.red);
            Vector3 dirToTarget = (targetPos - pos).normalized;
            Gizmos.DrawRay(pos, dirToTarget * 5f);
        }
    
        void DrawLineOfSightGizmos()
        {
            if (Target == Vector2.zero || !ship.Weapons.Primary) return;

            Vector3 firePos = ship.Weapons.Primary.firePoint ? ship.Weapons.Primary.firePoint.position : transform.position;
            Vector3 targetPos = GamePlane.PlanePointToWorld(Target);
        
            // Line of sight ray
            bool hasLOS = HasLineOfSight();
            Gizmos.color = hasLOS ? Color.green : Color.red;
            Gizmos.DrawLine(firePos, targetPos);
        
            // Fire point marker
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePos, 0.5f);
        
            // Angle tolerance before raycast visualization
            Vector3 forward = ship?.CurrentState.Kinematics.Forward ?? Vector2.up;
            forward = new Vector3(forward.x, forward.y, 0f);
        
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            DrawAngleCone(firePos, forward, angleToleranceBeforeRay, fireDistance);
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
