using System;
using AI.Context;
using Movement;
using Ships;
using UnityEngine;

namespace AI.Tactics
{
    /// <summary>
    /// What one ship reads about its fight before it picks a tactic, from its own sensors only: its
    /// target, whether it sits in the target's guns, the allies in its firing lane, the target's open
    /// side, and its own shield, hull and hit clock. A pure read of <see cref="AIContext"/>: allies count
    /// by where they are, never by what another ship's brain is doing, and the hit clock on the context
    /// is the only state kept between reads.
    /// https://github.com/amindell11/astronomical-home/issues/1349
    /// </summary>
    public readonly struct Situation
    {
        public readonly bool hasTarget;
        public readonly float distance;
        /// <summary>Positive while the gap to the target shrinks.</summary>
        public readonly float closingSpeed;
        public readonly bool inMyRange;
        public readonly bool exposed;
        public readonly int alliesInLane;
        /// <summary>Unit direction off the target toward its open side; zero when neither side is open.</summary>
        public readonly Vector2 openSide;
        public readonly float shieldPct;
        public readonly float hullPct;
        public readonly float secondsSinceHit;

        private Situation(AIContext context, bool hasTarget = false, float distance = 0f, float closingSpeed = 0f,
            bool inMyRange = false, bool exposed = false, int alliesInLane = 0, Vector2 openSide = default)
        {
            this.hasTarget = hasTarget;
            this.distance = distance;
            this.closingSpeed = closingSpeed;
            this.inMyRange = inMyRange;
            this.exposed = exposed;
            this.alliesInLane = alliesInLane;
            this.openSide = openSide;
            shieldPct = context.Self.ShieldPct;
            hullPct = context.Self.HealthPct;
            secondsSinceHit = context.SecondsSinceHit;
        }

        public static Situation Read(AIContext context, TacticsSettings settings)
        {
            if (!context.Combat.HasEnemy) return new Situation(context);

            var me = context.Self.Kinematics;
            var target = context.Combat.Enemy;
            var them = target.Kinematics;
            var toTarget = them.pos - me.pos;
            var distance = toTarget.magnitude;
            var line = toTarget.normalized;
            var halfCone = settings.exposureCone * 0.5f;

            return new Situation(context,
                hasTarget: true,
                distance: distance,
                closingSpeed: Vector2.Dot(me.vel - them.vel, line),
                inMyRange: distance <= PrimaryRange(Myself(context)),
                exposed: distance <= PrimaryRange(target) && Vector2.Angle(them.Forward, -line) <= halfCone,
                alliesInLane: AlliesInLane(context, me.pos, them.pos, settings.laneWidth),
                openSide: OpenSide(context, them, distance, -line, halfCone, settings.laneWidth));
        }

        // A quarter turn each way at my distance; with both open, the tail-ward one wins.
        private static Vector2 OpenSide(AIContext context, in Kinematics target, float distance,
            Vector2 bearing, float halfCone, float laneWidth)
        {
            var left = new Vector2(-bearing.y, bearing.x);
            var leftOpen = IsOpen(context, target, left, distance, halfCone, laneWidth);
            var rightOpen = IsOpen(context, target, -left, distance, halfCone, laneWidth);
            if (leftOpen && rightOpen) return Vector2.Dot(left, target.Forward) <= 0f ? left : -left;
            if (leftOpen) return left;
            return rightOpen ? -left : Vector2.zero;
        }

        private static bool IsOpen(AIContext context, in Kinematics target, Vector2 side, float distance,
            float halfCone, float laneWidth) =>
            Vector2.Angle(target.Forward, side) > halfCone
            && AlliesInLane(context, target.pos + side * distance, target.pos, laneWidth) == 0;

        private static int AlliesInLane(AIContext context, Vector2 from, Vector2 to, float laneWidth)
        {
            if (context.Scout.ShipScan is not { } scan) return 0;

            var registry = context.Scout.Registry;
            var self = context.Self.Id;
            var length = Vector2.Distance(from, to);
            var line = (to - from).normalized;
            var count = 0;
            for (var i = 0; i < scan.count; i++)
            {
                var id = scan.shipIds[i];
                if (!registry.IsFriendly(self, id) || !registry.TryGetShip(id, out var ally) || !ally) continue;
                var offset = ally.Kinematics.pos - from;
                var along = Vector2.Dot(offset, line);
                var across = Mathf.Abs(line.x * offset.y - line.y * offset.x);
                if (along > 0f && along < length && across <= laneWidth * 0.5f) count++;
            }
            return count;
        }

        private static Ship Myself(AIContext context) =>
            context.Scout.Registry.TryGetShip(context.Self.Id, out var me) && me
                ? me
                : throw new InvalidOperationException(
                    $"A ship reading its situation must be in its own scout's registry; {context.Self.Id} is not.");

        private static float PrimaryRange(Ship ship) =>
            ship.Weapons && ship.Weapons.Primary ? ship.Weapons.Primary.FireRange : 0f;
    }
}
