using System.Collections.Generic;
using AI;
using Ships;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>The RL policy's commanded facing/velocity against the ship's actual nose (the facing-churn diagnostic, #222), on canvas primitives so it appears in filmed runs as well as live editor gizmos. Anchored commands are drawn in the enemy frame: facing offsets around the bearing-to-enemy (a rough stand-in for the MPC's led intercept anchor — the true anchor is not re-resolved here, richer anchored gizmos are deferred), velocity as its radial/tangential reconstruction. Each pair ship whose chooser is an <see cref="IPolicyReadout"/> is painted; commanders are cached at construction.</summary>
    public sealed class PolicyPainter : IDiagnosticPainter
    {
        private const float VelocityScale = 0.4f;
        private const float FacingRayLength = 3f;

        private readonly List<AICommander> commanders = new();

        public PolicyPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.Policy;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var commander in commanders) Draw(canvas, commander);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var commander = ship.GetComponentInChildren<AICommander>();
            if (commander && commander.Brain?.Chooser is IPolicyReadout)
                commanders.Add(commander);
        }

        public static void Draw(IDiagnosticCanvas canvas, AICommander commander)
        {
            if (commander.context == null) return;
            if (!(commander.Brain?.Chooser is IPolicyReadout readout) || readout.Count == 0) return;

            var kin = commander.context.Self.Kinematics;
            var combat = commander.context.Combat;
            // Bearing to the enemy as the facing anchor; nose as the fallback when there is no enemy.
            var losHat = combat != null && combat.HasEnemy
                ? SafeDir(combat.EnemyPos - kin.pos, kin.Forward)
                : kin.Forward;
            var anchorYaw = Mathf.Atan2(-losHat.x, losHat.y);
            var newest = readout.ActionFromNewest(0);

            DrawVelocity(canvas, kin.pos, newest, losHat);
            DrawCommandedFacing(canvas, kin.pos, anchorYaw, newest);
            DrawNose(canvas, kin.pos, kin.Forward);
            DrawLabel(canvas, commander, kin, anchorYaw, newest, readout);
        }

        // Enemy-frame velocity reconstruction: radial along the LOS, tangential CCW (the VelocityRebase basis), no enemy-velocity lead.
        private static void DrawVelocity(IDiagnosticCanvas canvas, Vector2 pos, PolicyAction newest, Vector2 losHat)
        {
            var tangentHat = new Vector2(losHat.y, -losHat.x);
            var vel = newest.radialSpeed * losHat + newest.tangentialSpeed * tangentHat;
            canvas.Line(pos, pos + vel * VelocityScale, new Color(0f, 1f, 1f, 0.8f));
        }

        private static void DrawCommandedFacing(IDiagnosticCanvas canvas, Vector2 pos, float anchorYaw, PolicyAction newest)
        {
            var dir = FacingDir(anchorYaw + newest.facingOffsetRad);
            canvas.Line(pos, pos + dir * (FacingRayLength * newest.facingWeight),
                new Color(1f, 0.1f, 0.8f, Mathf.Max(0.1f, newest.facingWeight)));
        }

        private static void DrawNose(IDiagnosticCanvas canvas, Vector2 pos, Vector2 forward) =>
            canvas.Line(pos, pos + forward * FacingRayLength, Color.green);

        private static void DrawLabel(IDiagnosticCanvas canvas, AICommander commander, Movement.Kinematics kin,
            float anchorYaw, PolicyAction newest, IPolicyReadout readout)
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

            var text = $"{rangeText}\n{churnText}\nWeight: {newest.facingWeight:F2}\nNose err: {noseErrorDeg:F1}°";
            canvas.Label(kin.pos + new Vector2(0f, 0.5f), text, Color.white, 3f);
        }

        private static Vector2 SafeDir(Vector2 v, Vector2 fallback) =>
            v.sqrMagnitude > 1e-8f ? v.normalized : fallback;

        // MPC facing convention: fwd = (-sin(yaw), cos(yaw)), yaw in radians.
        private static Vector2 FacingDir(float facingRad) => new(-Mathf.Sin(facingRad), Mathf.Cos(facingRad));
    }
}
