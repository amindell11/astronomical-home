#if UNITY_EDITOR
using Game;
using UnityEngine;
using Utils;

namespace Ships.Movement
{
    public partial class MovementController
    {
        private Vector2 dbgThrust, dbgStrafe, dbgBoost;
        private float dbgYaw;

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showMovementGizmos) return;

            var pos = transform.position;
            var scale = movementGizmoScale;

            // Thrust - Yellow Sphere
            if (dbgThrust.sqrMagnitude > 0.01f)
                SuperGizmos.DrawArrow(pos, GamePlane.PlaneDirToWorld(dbgThrust), 
                    SuperGizmos.HeadType.Sphere, 0.15f, Color.yellow, scale);

            // Strafe - Yellow Cube
            if (dbgStrafe.sqrMagnitude > 0.01f)
                SuperGizmos.DrawArrow(pos, GamePlane.PlaneDirToWorld(dbgStrafe), 
                    SuperGizmos.HeadType.Cube, 0.15f, Color.yellow, scale);

            // Boost - Cyan Sphere (Flashier)
            if (dbgBoost.sqrMagnitude > 0.01f)
                SuperGizmos.DrawArrow(pos, GamePlane.PlaneDirToWorld(dbgBoost), 
                    SuperGizmos.HeadType.Sphere, 0.25f, Color.cyan, scale * 1.5f);
            
            // Yaw Torque - Rotation indicator
            if (!(Mathf.Abs(dbgYaw) > 0.01f)) return;
            var color = dbgYaw > 0 ? Color.green : Color.red;
            Gizmos.color = color;
            var radius = 0.5f * scale;
            var angle = dbgYaw * 45f; // Scale for visibility
            SuperGizmos.DrawWireArc(pos, GamePlane.Normal, transform.up, angle, radius);
        }

        partial void DebugForces(Vector2 thrust, Vector2 strafe, Vector2 boost, float yaw)
        {
            dbgThrust = thrust;
            dbgStrafe = strafe;
            dbgBoost = boost;
            dbgYaw = yaw;
        }
    }
}
#endif
