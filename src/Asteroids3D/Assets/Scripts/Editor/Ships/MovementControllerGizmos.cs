using Game;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Ships.Movement
{
    /// <summary>Applied translational forces as arrows scaled by their settings maximum, and yaw torque as a nose ray plus swept arc. Head shape separates thrust from strafe where color alone would not.</summary>
    internal static class MovementControllerGizmos
    {
        private const float MinForce = 0.01f;
        private const float MaxSweepDeg = 45f;
        private const float ArcRadiusFactor = 0.5f;
        private const float HeadSize = 0.18f;

        [DrawGizmo(GizmoType.Selected, typeof(MovementController))]
        private static void DrawForces(MovementController mover, GizmoType gizmoType)
        {
            if (!Application.isPlaying) return;

            var origin = mover.transform.position;
            var scale = mover.movementGizmoScale;
            var settings = mover.settings;

            if (mover.dbgThrust.sqrMagnitude > MinForce)
                Arrow(origin, Fraction(mover.dbgThrust, settings?.forwardForce ?? 0f) * scale,
                    SuperGizmos.HeadType.Sphere, Color.yellow);

            if (mover.dbgStrafe.sqrMagnitude > MinForce)
                Arrow(origin, Fraction(mover.dbgStrafe, settings?.maxStrafeForce ?? 0f) * scale,
                    SuperGizmos.HeadType.Cube, Color.yellow);

            if (mover.dbgBoost.sqrMagnitude > MinForce)
                Arrow(origin, Fraction(mover.dbgBoost, settings?.boostImpulse ?? 0f) * scale * 1.5f,
                    SuperGizmos.HeadType.Sphere, Color.cyan);

            if (Mathf.Abs(mover.dbgYaw) > MinForce)
                DrawYawTorque(mover, origin, scale, settings?.yawTorque ?? 0f);
        }

        private static void DrawYawTorque(MovementController mover, Vector3 origin, float scale, float maxTorque)
        {
            var nose = GamePlane.WorldDirToPlane(mover.transform.up);
            if (nose.sqrMagnitude < 1e-6f) return;
            nose.Normalize();

            var color = mover.dbgYaw > 0f ? Color.green : Color.red;
            var radius = ArcRadiusFactor * scale;
            var sweepDeg = Mathf.Clamp(mover.dbgYaw / (maxTorque > 0f ? maxTorque : 1f), -2f, 2f) * MaxSweepDeg;
            var noseWorld = GamePlane.PlaneDirToWorld(nose);

            Gizmos.color = color;
            Gizmos.DrawLine(origin, origin + noseWorld * radius);
            SuperGizmos.DrawWireArc(origin, GamePlane.Rotation * Vector3.forward, noseWorld, sweepDeg, radius);
        }

        private static void Arrow(Vector3 origin, Vector2 planeVector, SuperGizmos.HeadType head, Color color) =>
            SuperGizmos.DrawArrow(origin, GamePlane.PlaneDirToWorld(planeVector), head, HeadSize, color);

        private static Vector2 Fraction(Vector2 force, float max) => force / (max > 0f ? max : 1f);
    }
}
