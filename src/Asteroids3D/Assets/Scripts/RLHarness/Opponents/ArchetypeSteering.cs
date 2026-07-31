using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Deterministic border handling for the scripted opponent archetypes, as one pure velocity-law post-step (the <see cref="RangerChooser.HoldRangeVelocity"/> style).</summary>
    public static class ArchetypeSteering
    {
        // At maxSpeed 25 with ~18 u/s² braking the tangent-point overshoot is ~17 u — stay well inside.
        public const float BorderMargin = 60f;

        /// <summary>Inside the edge margin, rotates an outward-bound commanded velocity toward the border tangent — full tangent at half the margin depth, bending on toward inward by the border itself (momentum headroom) — preserving speed; inward-bound commands pass through.</summary>
        public static Vector2 BorderTangentSteer(Vector2 planePos, Vector2 commandedVel,
            Vector2 arenaCenter, float borderRadius, float margin)
        {
            var radial = planePos - arenaCenter;
            var r = radial.magnitude;
            var inner = borderRadius - margin;
            var speed = commandedVel.magnitude;
            if (r <= inner || r < 1e-4f || speed < 1e-4f) return commandedVel;

            var outwardHat = radial / r;
            if (Vector2.Dot(commandedVel, outwardHat) <= 0f) return commandedVel;

            var perp = new Vector2(-outwardHat.y, outwardHat.x);
            var tangentHat = Vector2.Dot(commandedVel, perp) >= 0f ? perp : -perp;
            var t = (r - inner) / (0.5f * margin);
            var dir = t <= 1f
                ? Vector2.Lerp(commandedVel / speed, tangentHat, t)
                : Vector2.Lerp(tangentHat, -outwardHat, Mathf.Clamp01(t - 1f));
            return speed * dir.normalized;
        }
    }
}
