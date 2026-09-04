using AI;
using Combat;
using Game;
using Game.Diagnostics;
using Ships.Command;
using UnityEditor;
using UnityEngine;
using Utils;
using Combat.Targeting;

namespace Ships
{
    /// <summary>One selected ship's combat picture in plane space: velocity arrow, speed readout, the gunner's exact intercept aim colored by the primary weapon's envelope, and the primary fire-range ring. Selecting a second ship adds the pair's line of sight.</summary>
    [InitializeOnLoad]
    internal static class ShipGizmos
    {
        static ShipGizmos() =>
            GizmoView.Register(typeof(Ship), "combat", "Combat Picture",
                "velocity arrow, speed, envelope-colored aim + fire-range ring; LOS to 2nd selected", "Combat");

        private const float VelocitySecondsShown = 0.6f;
        private const float VelocityHeadSize = 0.4f;
        private const float RangeRingDim = 0.55f;

        private static readonly Color ShipAColor = new(1f, 0.55f, 0.15f);
        private static readonly Color ShipBColor = new(0.2f, 0.85f, 1f);
        private static readonly Color EnvelopeOpen = new(0.25f, 1f, 0.3f);
        private static readonly Color EnvelopeClosed = new(0.55f, 0.55f, 0.55f);
        private static readonly Color LosClear = new(1f, 1f, 1f, 0.35f);
        private static readonly Color LosBlocked = new(1f, 0.25f, 0.2f);

        private static readonly Vector3 PlaneNormal = GamePlane.Rotation * Vector3.forward;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(Ship))]
        private static void Draw(Ship ship, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(Ship), "combat") || !GizmoView.InScope(ship)) return;
            // Velocity, weapons and the gunner's lead all come from Awake-built state.
            if (!Application.isPlaying) return;

            var enemy = OtherSelectedShip(ship);
            // Instance-id order keeps each ship's color stable and draws the shared line once.
            var first = !enemy || ship.GetInstanceID() < enemy.GetInstanceID();

            DrawShip(ship, first ? ShipAColor : ShipBColor);
            if (!enemy) return;

            DrawAim(ship, enemy);
            if (first) DrawLos(ship, enemy);
        }

        private static void DrawShip(Ship ship, Color color)
        {
            var k = ship.Kinematics;
            SuperGizmos.DrawArrow(GamePlane.PlanePointToWorld(k.pos),
                GamePlane.PlaneDirToWorld(k.vel * VelocitySecondsShown),
                SuperGizmos.HeadType.Sphere, VelocityHeadSize, color);
            ShipReadout.Draw(k.pos, ShipReadoutRow.Speed, $"{k.vel.magnitude:0.0}", color);

            var range = ship.Weapons && ship.Weapons.Primary ? ship.Weapons.Primary.FireRange : 0f;
            if (range > 0f) Ring(k.pos, range, color * RangeRingDim);
        }

        private static void DrawAim(Ship ship, Ship enemy)
        {
            var context = ship.Weapons ? ship.Weapons.Context : null;
            var sight = context?.Sight(WeaponSlot.Primary);
            if (sight == null) return;

            var k = ship.Kinematics;
            var ek = enemy.Kinematics;
            var aim = Gunner.AimPoint(in k, ek.pos, ek.vel, context.ProjectileSpeed(WeaponSlot.Primary));
            // InEnvelope, never Evaluate — Evaluate mutates the firing path's LOS cache.
            var inEnvelope = sight.InEnvelope(GamePlane.PlanePointToWorld(aim));
            Line(k.pos, aim, inEnvelope ? EnvelopeOpen : EnvelopeClosed);
        }

        private static void DrawLos(Ship ship, Ship enemy)
        {
            var from = ship.Kinematics.pos;
            var to = enemy.Kinematics.pos;
            var clear = TargetingMath.IsLineClear(GamePlane.PlanePointToWorld(from), GamePlane.PlanePointToWorld(to));
            Line(from, to, clear ? LosClear : LosBlocked);
        }

        private static Ship OtherSelectedShip(Ship ship)
        {
            var selected = Selection.GetFiltered<Ship>(SelectionMode.Unfiltered);
            if (selected.Length != 2) return null;
            return selected[0] == ship ? selected[1] : selected[0];
        }

        private static void Ring(Vector2 center, float radius, Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(GamePlane.PlanePointToWorld(center), PlaneNormal, radius);
        }

        private static void Line(Vector2 a, Vector2 b, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
        }
    }
}
