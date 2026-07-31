using System.Collections.Generic;
using AI;
using Ships;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>The RL policy's commanded velocity/facing fan against the ship's actual nose (the facing-churn diagnostic, #222), on canvas primitives so it appears in filmed runs as well as live editor gizmos. Each pair ship whose chooser is an <see cref="IPolicyReadout"/> is painted; commanders are cached at construction.</summary>
    public sealed class PolicyPainter : IDiagnosticPainter
    {
        private const float VelocityScale = 0.4f;
        private const float FacingRayLength = 3f;
        private const float FanRayLength = 2.2f;
        private const int CaptureFanDepth = 10;

        private readonly List<AICommander> commanders = new();

        public PolicyPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.Policy;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var commander in commanders) Draw(canvas, commander, CaptureFanDepth);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var commander = ship.GetComponentInChildren<AICommander>();
            if (commander && commander.Brain?.Chooser is IPolicyReadout)
                commanders.Add(commander);
        }

        public static void Draw(IDiagnosticCanvas canvas, AICommander commander, int fanDepth)
        {
            if (commander.context == null) return;
            if (!(commander.Brain?.Chooser is IPolicyReadout readout) || readout.Count == 0) return;

            var kin = commander.context.Self.Kinematics;
            var newest = readout.ActionFromNewest(0);

            DrawVelocity(canvas, kin.pos, newest.worldVelocity);
            DrawFan(canvas, kin.pos, readout, fanDepth);
            DrawCommandedFacing(canvas, kin.pos, newest);
            DrawNose(canvas, kin.pos, kin.Forward);
            DrawLabel(canvas, commander, kin, newest, readout);
        }

        private static void DrawVelocity(IDiagnosticCanvas canvas, Vector2 pos, Vector2 worldVelocity) =>
            canvas.Line(pos, pos + worldVelocity * VelocityScale, new Color(0f, 1f, 1f, 0.8f));

        private static void DrawFan(IDiagnosticCanvas canvas, Vector2 pos, IPolicyReadout readout, int fanDepth)
        {
            var fanCount = Mathf.Min(fanDepth, readout.Count);
            if (fanCount <= 0) return;

            var denom = Mathf.Max(fanCount - 1, 1);
            for (var i = 0; i < fanCount; i++)
            {
                var action = readout.ActionFromNewest(i);
                var alpha = Mathf.Lerp(0.7f, 0.03f, i / (float)denom);
                canvas.Line(pos, pos + FacingDir(action.facingRad) * FanRayLength, new Color(1f, 0.55f, 0f, alpha));
            }
        }

        private static void DrawCommandedFacing(IDiagnosticCanvas canvas, Vector2 pos, PolicyAction newest)
        {
            var dir = FacingDir(newest.facingRad);
            canvas.Line(pos, pos + dir * (FacingRayLength * newest.facingWeight),
                new Color(1f, 0.1f, 0.8f, Mathf.Max(0.1f, newest.facingWeight)));
        }

        private static void DrawNose(IDiagnosticCanvas canvas, Vector2 pos, Vector2 forward) =>
            canvas.Line(pos, pos + forward * FacingRayLength, Color.green);

        private static void DrawLabel(IDiagnosticCanvas canvas, AICommander commander, Movement.Kinematics kin,
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
            canvas.Label(kin.pos + new Vector2(0f, 0.5f), text, Color.white, 3f);
        }

        // MPC facing convention: fwd = (-sin(yaw), cos(yaw)), yaw in radians.
        private static Vector2 FacingDir(float facingRad) => new(-Mathf.Sin(facingRad), Mathf.Cos(facingRad));
    }
}
