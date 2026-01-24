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
            {
                float maxF = (settings != null && settings.forwardForce > 0) ? settings.forwardForce : 1f;
                var dir = GamePlane.PlaneDirToWorld(dbgThrust / maxF);
                SuperGizmos.DrawArrow(pos, dir, SuperGizmos.HeadType.Sphere, 0.15f, Color.yellow, scale);
            }

            // Strafe - Yellow Cube
            if (dbgStrafe.sqrMagnitude > 0.01f)
            {
                float maxS = (settings != null && settings.maxStrafeForce > 0) ? settings.maxStrafeForce : 1f;
                var dir = GamePlane.PlaneDirToWorld(dbgStrafe / maxS);
                SuperGizmos.DrawArrow(pos, dir, SuperGizmos.HeadType.Cube, 0.15f, Color.yellow, scale);
            }

            // Boost - Cyan Sphere (Flashier)
            if (dbgBoost.sqrMagnitude > 0.01f)
            {
                float maxB = (settings != null && settings.boostImpulse > 0) ? settings.boostImpulse : 1f;
                var dir = GamePlane.PlaneDirToWorld(dbgBoost / maxB);
                SuperGizmos.DrawArrow(pos, dir, SuperGizmos.HeadType.Sphere, 0.25f, Color.cyan, scale * 1.5f);
            }
            
            // Yaw Torque - Rotation indicator
            if (Mathf.Abs(dbgYaw) > 0.01f)
            {
                var color = dbgYaw > 0 ? Color.green : Color.red;
                Gizmos.color = color;
                var radius = 0.5f * scale;
                
                float maxTorque = (settings != null && settings.yawTorque > 0) ? settings.yawTorque : 1f;
                // Clamp and normalize indicator
                var torquePct = Mathf.Clamp(dbgYaw / maxTorque, -2f, 2f);
                var angle = torquePct * 45f; 

                var normal = GamePlane.Normal;
                // Ensure we have a valid projected nose even if the ship is tilting
                var nose = Vector3.ProjectOnPlane(transform.up, normal).normalized;
                
                if (nose.sqrMagnitude > 0.001f)
                {
                    // Draw a small line for the nose itself to show the reference
                    Gizmos.DrawRay(pos, nose * radius);
                    SuperGizmos.DrawWireArc(pos, normal, nose, angle, radius);
                }
            }
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
