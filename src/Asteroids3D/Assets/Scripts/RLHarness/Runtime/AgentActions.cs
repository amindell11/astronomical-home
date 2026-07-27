using AI.Observation;
using UnityEngine;

namespace Game.RLHarness
{
    public readonly struct AgentAction
    {
        public readonly Vector2 velocityEgo;
        public readonly Vector2 facingEgo;
        public readonly bool fire;
        public readonly bool boost;

        public AgentAction(Vector2 velocityEgo, Vector2 facingEgo, bool fire, bool boost)
        {
            this.velocityEgo = velocityEgo;
            this.facingEgo = facingEgo;
            this.fire = fire;
            this.boost = boost;
        }
    }

    /// <summary>Pure mapping between the 6-continuous action vector [vx, vy, fire, boost, fx, fy] ∈ [−1,1] and game-frame commands (fire/boost are threshold-gated at 0 — 4.0.3 rejects hybrid specs in the trainer path; fx/fy are a facing direction whose angle is consumed via <see cref="ToFacingRad"/> and whose magnitude is the facing authority via <see cref="ToFacingWeight"/>). Ego→world conversion happens ONCE per decision at the boundary; re-rotating per tick would feed live yaw back into the reference.</summary>
    public static class AgentActions
    {
        public const int Count = 6;
        public const float TriggerThreshold = 0f;

        public static AgentAction Map(float vx, float vy, float fire, float boost, float fx, float fy) => new(
            new Vector2(Mathf.Clamp(vx, -1f, 1f), Mathf.Clamp(vy, -1f, 1f)),
            new Vector2(fx, fy),
            fire > TriggerThreshold,
            boost > TriggerThreshold);

        public static Vector2 ToWorldVelocity(Vector2 velocityEgo, Vector2 forwardPlane, float maxSpeed) =>
            Vector2.ClampMagnitude(
                new EgoFrame(Vector2.zero, forwardPlane).PlaneDirection(velocityEgo) * maxSpeed, maxSpeed);

        /// <summary>Ego facing direction → commanded world-plane yaw in the MPC convention (fwd = (−sin, cos)). A degenerate direction holds the current nose.</summary>
        public static float ToFacingRad(Vector2 facingEgo, Vector2 forwardPlane)
        {
            var world = new EgoFrame(Vector2.zero, forwardPlane).PlaneDirection(facingEgo);
            if (world.sqrMagnitude < 1e-6f) world = forwardPlane;
            return Mathf.Atan2(-world.x, world.y);
        }

        /// <summary>Facing authority from the ego direction's magnitude: scales the MPC facing weight so the policy can express "don't care" (near-zero vectors have unstable angles AND near-zero weight). Plain clamp, no deadzone — a deadzone would re-add a discontinuity.</summary>
        public static float ToFacingWeight(Vector2 facingEgo) => Mathf.Clamp01(facingEgo.magnitude);

        /// <summary>Inverse of <see cref="ToWorldVelocity"/> for the heuristic: a world-plane velocity within maxSpeed maps back inside the [−1,1] action box.</summary>
        public static Vector2 ToEgoAction(Vector2 worldVelocity, Vector2 forwardPlane, float maxSpeed) =>
            maxSpeed > 0f
                ? new EgoFrame(Vector2.zero, forwardPlane).Direction(worldVelocity) / maxSpeed
                : Vector2.zero;
    }
}
