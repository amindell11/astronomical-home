using Game;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>The RL policy's commanded facing and velocity against the ship's actual nose — the facing-churn diagnostic. Anchored commands are drawn in the enemy frame: facing offsets around the bearing-to-enemy, velocity as its radial/tangential reconstruction.</summary>
    internal static class PolicyGizmos
    {
        private const float VelocityScale = 0.4f;
        private const float FacingRayLength = 3f;
        // The readout ring holds 16; beyond that the fan reads as noise.
        private const int FanDepth = 12;

        private static readonly Color Velocity = new(0f, 1f, 1f, 0.8f);
        private static readonly Color Fan = new(1f, 0.55f, 0.1f);

        [DrawGizmo(GizmoType.Selected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!Application.isPlaying || commander.context == null) return;
            if (commander.Brain is not IPolicyReadout readout || readout.Count == 0) return;

            var kin = commander.context.Self.Kinematics;
            var combat = commander.context.Combat;
            // Bearing to the enemy is the facing anchor; nose stands in when there is no enemy.
            var losHat = combat != null && combat.HasEnemy
                ? SafeDir(combat.EnemyPos - kin.pos, kin.Forward)
                : kin.Forward;
            var anchorYaw = Mathf.Atan2(-losHat.x, losHat.y);
            var newest = readout.ActionFromNewest(0);

            DrawHistoryFan(kin.pos, anchorYaw, readout);
            DrawVelocity(kin.pos, newest, losHat);
            DrawCommandedFacing(kin.pos, anchorYaw, newest);
            Line(kin.pos, kin.pos + kin.Forward * FacingRayLength, Color.green);
            DrawReadout(commander, kin, anchorYaw, newest, readout);
        }

        // Older commands fade, so a churning policy shows as a spread fan rather than one jittering ray.
        private static void DrawHistoryFan(Vector2 pos, float anchorYaw, IPolicyReadout readout)
        {
            var depth = Mathf.Min(FanDepth, readout.Count);
            for (var i = depth - 1; i >= 1; i--)
            {
                var action = readout.ActionFromNewest(i);
                var dir = FacingDir(anchorYaw + action.facingOffsetRad);
                var age = i / (float)depth;
                var color = Fan;
                color.a = Mathf.Max(0.05f, (1f - age) * 0.55f);
                Line(pos, pos + dir * (FacingRayLength * action.facingWeight), color);
            }
        }

        // Enemy-frame reconstruction: radial along the LOS, tangential CCW (the VelocityRebase basis), no enemy-velocity lead.
        private static void DrawVelocity(Vector2 pos, PolicyAction newest, Vector2 losHat)
        {
            var tangentHat = new Vector2(losHat.y, -losHat.x);
            var vel = newest.radialSpeed * losHat + newest.tangentialSpeed * tangentHat;
            Line(pos, pos + vel * VelocityScale, Velocity);
        }

        private static void DrawCommandedFacing(Vector2 pos, float anchorYaw, PolicyAction newest)
        {
            var dir = FacingDir(anchorYaw + newest.facingOffsetRad);
            Line(pos, pos + dir * (FacingRayLength * newest.facingWeight),
                new Color(1f, 0.1f, 0.8f, Mathf.Max(0.1f, newest.facingWeight)));
        }

        private static void DrawReadout(AICommander commander, Movement.Kinematics kin, float anchorYaw,
            PolicyAction newest, IPolicyReadout readout)
        {
            var combat = commander.context.Combat;
            var rangeText = combat != null && combat.HasEnemy
                ? $"Range: {Vector2.Distance(kin.pos, combat.EnemyPos):F1}"
                : "Range: -";

            var commandedDeg = (anchorYaw + newest.facingOffsetRad) * Mathf.Rad2Deg;
            var noseErrorDeg = Mathf.Abs(Mathf.DeltaAngle(commandedDeg, kin.yaw));

            var churnText = "Churn: -";
            if (readout.Count >= 2)
            {
                var prevOffsetDeg = readout.ActionFromNewest(1).facingOffsetRad * Mathf.Rad2Deg;
                var offsetDeg = newest.facingOffsetRad * Mathf.Rad2Deg;
                churnText = $"Churn: {Mathf.Abs(Mathf.DeltaAngle(prevOffsetDeg, offsetDeg)):F1}°";
            }

            ShipReadout.Draw(kin.pos, ShipReadoutRow.Policy,
                $"{rangeText}\n{churnText}\nWeight: {newest.facingWeight:F2}\nNose err: {noseErrorDeg:F1}°",
                Color.white);
        }

        private static void Line(Vector2 a, Vector2 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
        }

        private static Vector2 SafeDir(Vector2 v, Vector2 fallback) =>
            v.sqrMagnitude > 1e-8f ? v.normalized : fallback;

        // MPC facing convention: fwd = (-sin(yaw), cos(yaw)), yaw in radians.
        private static Vector2 FacingDir(float facingRad) => new(-Mathf.Sin(facingRad), Mathf.Cos(facingRad));
    }
}
