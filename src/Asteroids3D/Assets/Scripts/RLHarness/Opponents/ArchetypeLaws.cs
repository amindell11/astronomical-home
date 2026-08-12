using Movement;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The pure archetype velocity laws (the <see cref="RangerBrain.HoldRangeVelocity"/> style — the hold-range law itself lives there, doubling as the agent Heuristic's source policy).</summary>
    internal static class ArchetypeLaws
    {
        private const float JukeBlend = 0.6f;
        private const float RadialGain = 0.9f;
        // A tangential-only rotating command needs a standing radius error ∝ v²/r to supply
        // the centripetal demand through the P-term — feed it forward instead (Kff in seconds).
        private const float CentripetalKff = 2.5f;
        private const float MinCentripetalRange = 1f;

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
    }
}
