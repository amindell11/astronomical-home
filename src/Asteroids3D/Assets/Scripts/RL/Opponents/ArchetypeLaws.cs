using System.Collections.Generic;
using Combat.Targeting;
using Movement;
using UnityEngine;

namespace RL.Opponents
{
    /// <summary>The pure archetype laws: the velocity laws (the <see cref="RangerBrain.HoldRangeVelocity"/> style — the hold-range law itself lives there, doubling as the agent Heuristic's source policy) and the missile-evasion laws the brain blends over them.</summary>
    internal static class ArchetypeLaws
    {
        private const float JukeBlend = 0.6f;
        private const float RadialGain = 0.9f;
        // A tangential-only rotating command needs a standing radius error ∝ v²/r to supply
        // the centripetal demand through the P-term — feed it forward instead (Kff in seconds).
        private const float CentripetalKff = 2.5f;
        private const float MinCentripetalRange = 1f;
        // Sideways speed (u/s) below which the nose, not the drift, picks the escape side.
        private const float RestSpeed = 0.5f;

        /// <summary>The pure flee law: away from the threat, blended with the seeded tangential juke.</summary>
        internal static Vector2 FleeVelocity(Vector2 selfPos, Vector2 threatPos, int jukeSign, float speed)
        {
            var away = selfPos - threatPos;
            var fleeHat = away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.up;
            var dir = (fleeHat + JukeBlend * jukeSign * new Vector2(-fleeHat.y, fleeHat.x)).normalized;
            return speed * dir;
        }

        /// <summary>The pure orbit law: tangential command at the jittered speed plus a radial P-term with the centripetal feed-forward.</summary>
        internal static Vector2 OrbitVelocity(in Kinematics self, in Kinematics enemy, float orbitRadius,
            int orbitDirection, float speedFraction, float maxSpeed)
        {
            var los = enemy.pos - self.pos;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;
            var tangent = orbitDirection * new Vector2(-losHat.y, losHat.x);
            var vTan = speedFraction * maxSpeed;
            var centripetal = CentripetalKff * vTan * vTan / Mathf.Max(r, MinCentripetalRange);
            var radial = (RadialGain * (orbitRadius - r) - centripetal) * -losHat;
            return Vector2.ClampMagnitude(vTan * tangent + radial, maxSpeed);
        }

        /// <summary>Range over closing speed, the closing speed floored so a missile in its slow start doesn't read as far away.</summary>
        internal static float TimeToImpact(in Kinematics self, in Kinematics missile, float speedFloor)
        {
            var los = self.pos - missile.pos;
            var range = los.magnitude;
            var closing = range > 1e-4f ? Vector2.Dot(missile.vel - self.vel, los / range) : 0f;
            return range / Mathf.Max(closing, speedFloor);
        }

        /// <summary>Evasion urgency: 0 at the far time to impact, rising linearly to 1 at the near time.</summary>
        internal static float EvasionUrgency(float timeToImpact, float farTime, float nearTime) =>
            Mathf.InverseLerp(farTime, nearTime, timeToImpact);

        /// <summary>The tracker with the shortest time to impact.</summary>
        internal static ITrackingMissile MostUrgentTracker(in Kinematics self, IReadOnlyList<ITrackingMissile> trackers,
            float speedFloor, out float timeToImpact)
        {
            ITrackingMissile mostUrgent = null;
            timeToImpact = float.PositiveInfinity;
            foreach (var missile in trackers)
            {
                var t = TimeToImpact(in self, missile.Kinematics, speedFloor);
                if (t >= timeToImpact) continue;
                timeToImpact = t;
                mostUrgent = missile;
            }
            return mostUrgent;
        }

        /// <summary>Which side of the missile's line of sight to break to: the side the velocity leans, or the nose's side when the ship is near rest along it.</summary>
        internal static int EscapeSide(in Kinematics self, Vector2 missilePos)
        {
            var perp = LeftOfApproach(self.pos, missilePos);
            var lean = Vector2.Dot(perp, self.vel);
            if (Mathf.Abs(lean) < RestSpeed) lean = Vector2.Dot(perp, self.Forward);
            return lean >= 0f ? 1 : -1;
        }

        /// <summary>Square to the missile's line of sight, on the held side.</summary>
        internal static Vector2 EscapeHeading(Vector2 selfPos, Vector2 missilePos, int side) =>
            side * LeftOfApproach(selfPos, missilePos);

        /// <summary>The law velocity blended toward the escape velocity by urgency.</summary>
        internal static Vector2 EvasionVelocity(Vector2 lawVelocity, Vector2 escapeHeading, float escapeSpeed,
            float urgency) =>
            Vector2.Lerp(lawVelocity, escapeSpeed * escapeHeading, urgency);

        private static Vector2 LeftOfApproach(Vector2 selfPos, Vector2 missilePos)
        {
            var approach = selfPos - missilePos;
            var approachHat = approach.sqrMagnitude > 1e-8f ? approach.normalized : Vector2.up;
            return new Vector2(-approachHat.y, approachHat.x);
        }
    }
}
