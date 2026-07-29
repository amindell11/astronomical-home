using AI.Debug;
using Game;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>Visualizes the RL policy's commanded velocity/facing against the ship's actual
    /// nose, to test whether facing thrash tracks range (see the 2026-07-28 handoff).</summary>
    internal static class PolicyGizmos
    {
        private const float VelocityScale = 0.4f;
        private const float FacingRayLength = 3f;
        private const float FanRayLength = 2.2f;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!AIDebugContext.ShouldDraw(AIDebugChannel.Policy, gizmoType)) return;
            if (!Application.isPlaying || commander.context == null) return;
            if (!(commander.Brain?.Chooser is IPolicyReadout readout) || readout.Count == 0) return;

            var self = commander.context.Self;
            var kin = self.Kinematics;
            var shipWorld = GamePlane.PlanePointToWorld(kin.pos);
            var newest = readout.ActionFromNewest(0);

            DrawVelocity(shipWorld, newest.worldVelocity);
            DrawFan(shipWorld, readout);
            DrawCommandedFacing(shipWorld, newest);
            DrawNose(shipWorld, kin.Forward);
            DrawLabel(shipWorld, commander, kin, newest, readout);
        }

        private static void DrawVelocity(Vector3 shipWorld, Vector2 worldVelocity)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
            var vel = GamePlane.PlaneDirToWorld(worldVelocity);
            Gizmos.DrawRay(shipWorld, vel * VelocityScale);
        }

        private static void DrawFan(Vector3 shipWorld, IPolicyReadout readout)
        {
            var fanDepth = AIDebugContext.Settings ? AIDebugContext.Settings.policyFanDepth : 0;
            var fanCount = Mathf.Min(fanDepth, readout.Count);
            if (fanCount <= 0) return;

            var denom = Mathf.Max(fanCount - 1, 1);
            for (var i = 0; i < fanCount; i++)
            {
                var action = readout.ActionFromNewest(i);
                var alpha = Mathf.Lerp(0.7f, 0.03f, i / (float)denom);
                Gizmos.color = new Color(1f, 0.55f, 0f, alpha);
                var dir = FacingDir(action.facingRad);
                Gizmos.DrawRay(shipWorld, GamePlane.PlaneDirToWorld(dir) * FanRayLength);
            }
        }

        private static void DrawCommandedFacing(Vector3 shipWorld, PolicyAction newest)
        {
            var dir = FacingDir(newest.facingRad);
            Gizmos.color = new Color(1f, 0.1f, 0.8f, Mathf.Max(0.1f, newest.facingWeight));
            Gizmos.DrawRay(shipWorld, GamePlane.PlaneDirToWorld(dir) * (FacingRayLength * newest.facingWeight));
        }

        private static void DrawNose(Vector3 shipWorld, Vector2 forward)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(shipWorld, GamePlane.PlaneDirToWorld(forward) * FacingRayLength);
        }

        private static void DrawLabel(Vector3 shipWorld, AICommander commander, Movement.Kinematics kin,
            PolicyAction newest, IPolicyReadout readout)
        {
            var combat = commander.context.Combat;
            var rangeText = combat != null && combat.HasEnemy
                ? $"Range: {Vector2.Distance(kin.pos, combat.EnemyPos):F1}"
                : "Range: -";

            var commandedDeg = newest.facingRad * Mathf.Rad2Deg;
            var noseErrorDeg = Mathf.Abs(Mathf.DeltaAngle(commandedDeg, kin.yaw));

            var churnText = "Churn: -";
            if (readout.Count >= 2)
            {
                var prevDeg = readout.ActionFromNewest(1).facingRad * Mathf.Rad2Deg;
                churnText = $"Churn: {Mathf.Abs(Mathf.DeltaAngle(prevDeg, commandedDeg)):F1}°";
            }

            var text = $"{rangeText}\n{churnText}\nWeight: {newest.facingWeight:F2}\nNose err: {noseErrorDeg:F1}°";
            Handles.Label(shipWorld + Vector3.up * 0.5f, text,
                new GUIStyle { normal = { textColor = Color.white }, fontSize = 10 });
        }

        // MPC facing convention: fwd = (-sin(yaw), cos(yaw)), yaw in radians.
        private static Vector2 FacingDir(float facingRad) => new(-Mathf.Sin(facingRad), Mathf.Cos(facingRad));
    }
}
