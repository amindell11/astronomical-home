using Game;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Ships.Movement
{
    internal static class MovementControllerGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(MovementController))]
        private static void DrawForces(MovementController mover, GizmoType gizmoType)
        {
            if (!Application.isPlaying || !mover.showMovementGizmos) return;

            var pos = mover.transform.position;
            var scale = mover.movementGizmoScale;
            var settings = mover.settings;

            if (mover.dbgThrust.sqrMagnitude > 0.01f)
            {
                var maxF = (settings != null && settings.forwardForce > 0) ? settings.forwardForce : 1f;
                var dir = GamePlane.PlaneDirToWorld(mover.dbgThrust / maxF);
                SuperGizmos.DrawArrow(pos, dir, SuperGizmos.HeadType.Sphere, 0.15f, Color.yellow, scale);
            }

            if (mover.dbgStrafe.sqrMagnitude > 0.01f)
            {
                var maxS = (settings != null && settings.maxStrafeForce > 0) ? settings.maxStrafeForce : 1f;
                var dir = GamePlane.PlaneDirToWorld(mover.dbgStrafe / maxS);
                SuperGizmos.DrawArrow(pos, dir, SuperGizmos.HeadType.Cube, 0.15f, Color.yellow, scale);
            }

            if (mover.dbgBoost.sqrMagnitude > 0.01f)
            {
                var maxB = (settings != null && settings.boostImpulse > 0) ? settings.boostImpulse : 1f;
                var dir = GamePlane.PlaneDirToWorld(mover.dbgBoost / maxB);
                SuperGizmos.DrawArrow(pos, dir, SuperGizmos.HeadType.Sphere, 0.25f, Color.cyan, scale * 1.5f);
            }

            if (Mathf.Abs(mover.dbgYaw) > 0.01f)
                DrawYawTorque(mover, pos, scale, settings);
        }

        private static void DrawYawTorque(MovementController mover, Vector3 pos, float scale, ResolvedShipStats settings)
        {
            Gizmos.color = mover.dbgYaw > 0 ? Color.green : Color.red;
            var radius = 0.5f * scale;

            var maxTorque = (settings != null && settings.yawTorque > 0) ? settings.yawTorque : 1f;
            var torquePct = Mathf.Clamp(mover.dbgYaw / maxTorque, -2f, 2f);
            var angle = torquePct * 45f;

            var normal = GamePlane.Normal;
            var nose = Vector3.ProjectOnPlane(mover.transform.up, normal).normalized;

            if (nose.sqrMagnitude > 0.001f)
            {
                Gizmos.DrawRay(pos, nose * radius);
                SuperGizmos.DrawWireArc(pos, normal, nose, angle, radius);
            }
        }
    }
}
