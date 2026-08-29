using Game;
using Game.Diagnostics;
using Movement.MPC;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace AI
{
    /// <summary>The intent sentence as the solver resolved it this tick: one element per armed slot,
    /// anchored to the slot's true bound referent, with |weight| driving visibility so near-zero slots
    /// vanish. Geometry comes from the solver's own step-0 resolution (Cost.EvalContext), never a
    /// drawer-side reconstruction — what draws is what the MPC chases. Replaces the anchored-intent
    /// policy drawer, whose enemy-LOS anchor misdrew rock-bound slots. Readout row needs the brain's
    /// IPolicyReadout; the world geometry draws for any armed sentence (hand rows included).</summary>
    internal static class IntentGizmos
    {
        private const float MinVisibleWeight = 0.05f;
        private const float AimRayLength = 3f;
        private const float VelocityScale = 0.4f;
        private const float PosMarkerHalf = 0.5f;
        private const float BasisFwdTick = 1.5f;
        private const float BasisSideTick = 1f;
        private const float DottedScreenSize = 4f;

        // One ring radius per slot so shared referents read as concentric, not overdrawn.
        private const float AimRingRadius = 1.9f;
        private const float PosRingRadius = 2.3f;
        private const float VelRingRadius = 2.7f;

        private static readonly Color AimColor = new(1f, 0.1f, 0.8f);
        private static readonly Color PosColor = new(1f, 0.55f, 0.1f);
        private static readonly Color VelColor = new(0f, 1f, 1f);
        private static readonly Color LaneColor = new(0.6f, 0.3f, 1f);
        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        [DrawGizmo(GizmoType.Selected, typeof(AICommander))]
        private static void Draw(AICommander commander, GizmoType gizmoType)
        {
            if (!Application.isPlaying || commander.context == null) return;
            var nav = commander.Navigator;
            if (nav == null || nav.solver == null || nav.mpc == null) return;

            var sentence = nav.sentence;
            var s0 = nav.lastInitialState;
            var input = nav.solver.BuildCostInput(nav.CostVelocityReference, nav.enemyPos, nav.enemyVel,
                nav.enemyYaw, nav.enemyYawRate, nav.projectileSpeed, s0.vel, sentence,
                nav.referent1, nav.referent2, nav.referent3);
            var ctx = Cost.EvalContext.Create(s0, input, nav.config, 0);
            var shipPos = Plane(s0.pos);

            if (sentence.aim.armed) DrawAim(nav, shipPos, sentence.aim, ctx);
            if (sentence.pos.armed) DrawPos(nav, shipPos, sentence.pos, ctx);
            if (sentence.vel.armed) DrawVel(nav, shipPos, sentence.vel, ctx, input, s0);
            if (sentence.lane.armed) DrawLane(sentence.lane, ctx);
            DrawReadout(commander, nav, shipPos, s0, ctx);
        }

        private static void DrawAim(Navigator nav, Vector2 shipPos, in AimSlot aim, in Cost.EvalContext ctx)
        {
            if (Mathf.Abs(aim.weight) < MinVisibleWeight || float.IsNaN(ctx.facingTarget)) return;
            var color = Weighted(AimColor, aim.weight);
            ThickLine(shipPos, shipPos + FacingDir(ctx.facingTarget) * AimRayLength, color, aim.weight);
            if (SeatPosition(nav, aim.referent, out var seat))
                Ring(seat, AimRingRadius, color);
        }

        private static void DrawPos(Navigator nav, Vector2 shipPos, in PosSlot pos, in Cost.EvalContext ctx)
        {
            if (Mathf.Abs(pos.weight) < MinVisibleWeight || ctx.posWeightScale == 0f) return;
            var color = Weighted(PosColor, pos.weight);
            var point = Plane(ctx.posPoint);

            Cross(point, PosMarkerHalf, color, pos.weight);
            if (ctx.posSetpoint > 0.05f) Ring(point, ctx.posSetpoint, color);
            if (pos.weight > 0f) ThickLine(shipPos, point, color, pos.weight);
            else DottedLine(shipPos, point, color);
            if (SeatPosition(nav, pos.referent, out var seat))
                Ring(seat, PosRingRadius, color);
        }

        private static void DrawVel(Navigator nav, Vector2 shipPos, in VelSlot vel, in Cost.EvalContext ctx,
            CostInput input, State s0)
        {
            if (Mathf.Abs(vel.weight) < MinVisibleWeight || ctx.velTrackScale == 0f) return;
            var color = Weighted(VelColor, vel.weight);
            var target = shipPos + Plane(ctx.velocityRef) * VelocityScale;
            ThickLine(shipPos, target, color, vel.weight);
            Dot(target, 0.15f, color);

            if (!SeatPosition(nav, vel.referent, out var seat)) return;
            Ring(seat, VelRingRadius, color);

            // Basis ticks probed from the solver's own resolution — unit-(vr,vt) deltas around the
            // zeroed slot — so the drawn frame cannot drift from the frame the cost actually uses.
            var probe = input;
            probe.sentence.vel.radialSpeed = 0f;
            probe.sentence.vel.tangentialSpeed = 0f;
            var refOnly = Cost.EvalContext.Create(s0, probe, nav.config, 0).velocityRef;
            probe.sentence.vel.radialSpeed = 1f;
            var fwd = Cost.EvalContext.Create(s0, probe, nav.config, 0).velocityRef - refOnly;
            probe.sentence.vel.radialSpeed = 0f;
            probe.sentence.vel.tangentialSpeed = 1f;
            var side = Cost.EvalContext.Create(s0, probe, nav.config, 0).velocityRef - refOnly;

            var faint = color;
            faint.a = 0.35f;
            Line(seat, seat + Plane(fwd) * BasisFwdTick, faint);
            Line(seat, seat + Plane(side) * BasisSideTick, faint);
        }

        private static void DrawLane(in LaneSlot lane, in Cost.EvalContext ctx)
        {
            if (Mathf.Abs(lane.weight) < MinVisibleWeight || ctx.laneWeightScale == 0f) return;
            var color = Weighted(LaneColor, lane.weight);
            if (lane.weight > 0f) ThickLine(Plane(ctx.laneStart), Plane(ctx.laneEnd), color, lane.weight);
            else DottedLine(Plane(ctx.laneStart), Plane(ctx.laneEnd), color);
        }

        private static void DrawReadout(AICommander commander, Navigator nav, Vector2 shipPos, State s0,
            in Cost.EvalContext ctx)
        {
            if (!commander.Brain) return;
            var readout = commander.Brain as IPolicyReadout;
            if (readout == null || readout.Count == 0) return;
            var a = readout.ActionFromNewest(0);

            var phiDeg = a.facingOffsetRad * Mathf.Rad2Deg;
            var churn = "-";
            if (readout.Count >= 2)
            {
                var prev = readout.ActionFromNewest(1);
                churn = prev.aimReferent == a.aimReferent
                    ? Mathf.Abs(Mathf.DeltaAngle(prev.facingOffsetRad * Mathf.Rad2Deg, phiDeg)).ToString("F0") + "°"
                    : $"{R(prev.aimReferent)}→{R(a.aimReferent)}";
            }
            var nose = float.IsNaN(ctx.facingTarget)
                ? "-"
                : Mathf.Abs(Mathf.DeltaAngle(ctx.facingTarget * Mathf.Rad2Deg, s0.yaw * Mathf.Rad2Deg)).ToString("F0") + "°";
            var combat = commander.context.Combat;
            var range = combat != null && combat.HasEnemy
                ? Vector2.Distance(shipPos, combat.EnemyPos).ToString("F1")
                : "-";

            ShipReadout.Draw(shipPos, ShipReadoutRow.Policy,
                $"AIM→{R(a.aimReferent)} w{a.facingWeight:F2} φ{phiDeg:+0;-0}°\n"
                + $"POS→{R(a.posReferent)}/{F(a.posFrame)} w{a.posWeight:+0.00;-0.00}  VEL→{R(a.velReferent)}/{F(a.velFrame)} w{a.velocityWeight:F2}\n"
                + $"LN w{a.laneWeight:+0.00;-0.00}  FLD w{a.fieldWeight:F2}{(a.firePrimary ? " FIRE" : "")}{(a.boost ? " BST" : "")}\n"
                + $"Rng {range}  Churn {churn}  Nose {nose}",
                Color.white);
        }

        // Readout referents are the action's obs-slot choices (E/R1..R6); world anchors below use the
        // sentence's resolved seats — the two deliberately differ (IPolicyReadout never resolves rocks).
        private static string R(int choice) => choice == 0 ? "E" : "R" + choice;

        private static string F(int frame) => frame == 1 ? "F" : frame == 2 ? "V" : "P";

        private static bool SeatPosition(Navigator nav, int referent, out Vector2 pos)
        {
            switch (referent)
            {
                case 1: pos = Plane(nav.referent1.pos); return nav.referent1.valid;
                case 2: pos = Plane(nav.referent2.pos); return nav.referent2.valid;
                case 3: pos = Plane(nav.referent3.pos); return nav.referent3.valid;
                default: pos = Plane(nav.enemyPos); return !float.IsNaN(nav.enemyYaw);
            }
        }

        private static Color Weighted(Color color, float weight)
        {
            color.a = 0.25f + 0.75f * Mathf.Min(Mathf.Abs(weight), 1f);
            return color;
        }

        private static void ThickLine(Vector2 a, Vector2 b, Color color, float weight)
        {
            Handles.color = color;
            Handles.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b),
                1f + 3f * Mathf.Min(Mathf.Abs(weight), 1f));
        }

        private static void DottedLine(Vector2 a, Vector2 b, Color color)
        {
            Handles.color = color;
            Handles.DrawDottedLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b), DottedScreenSize);
        }

        private static void Line(Vector2 a, Vector2 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
        }

        private static void Cross(Vector2 center, float half, Color color, float weight)
        {
            ThickLine(center + new Vector2(-half, -half), center + new Vector2(half, half), color, weight);
            ThickLine(center + new Vector2(-half, half), center + new Vector2(half, -half), color, weight);
        }

        private static void Dot(Vector2 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawSolidDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius);
        }

        private static void Ring(Vector2 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius);
        }

        private static Vector2 Plane(float2 p) => new(p.x, p.y);

        // MPC facing convention: fwd = (-sin(yaw), cos(yaw)), yaw in radians.
        private static Vector2 FacingDir(float yaw) => new(-Mathf.Sin(yaw), Mathf.Cos(yaw));
    }
}
