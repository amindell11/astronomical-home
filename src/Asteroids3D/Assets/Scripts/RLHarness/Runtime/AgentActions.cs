using AI.Observation;
using UnityEngine;

namespace Game.RLHarness
{
    public readonly struct AgentAction
    {
        public readonly Vector2 velocityEgo;
        public readonly bool fire;
        public readonly bool boost;

        public AgentAction(Vector2 velocityEgo, bool fire, bool boost)
        {
            this.velocityEgo = velocityEgo;
            this.fire = fire;
            this.boost = boost;
        }
    }

    /// <summary>Pure mapping between the 4-continuous action vector [vx, vy, fire, boost] ∈ [−1,1] and game-frame commands (fire/boost are threshold-gated at 0 — 4.0.3 rejects hybrid specs in the trainer path). Ego→world conversion happens ONCE per decision at the boundary; re-rotating per tick would feed live yaw back into the reference.</summary>
    public static class AgentActions
    {
        public const int Count = 4;
        public const float TriggerThreshold = 0f;

        public static AgentAction Map(float vx, float vy, float fire, float boost) => new(
            new Vector2(Mathf.Clamp(vx, -1f, 1f), Mathf.Clamp(vy, -1f, 1f)),
            fire > TriggerThreshold,
            boost > TriggerThreshold);

        public static Vector2 ToWorldVelocity(Vector2 velocityEgo, Vector2 forwardPlane, float maxSpeed) =>
            Vector2.ClampMagnitude(
                new EgoFrame(Vector2.zero, forwardPlane).PlaneDirection(velocityEgo) * maxSpeed, maxSpeed);

        /// <summary>Inverse of <see cref="ToWorldVelocity"/> for the heuristic: a world-plane velocity within maxSpeed maps back inside the [−1,1] action box.</summary>
        public static Vector2 ToEgoAction(Vector2 worldVelocity, Vector2 forwardPlane, float maxSpeed) =>
            maxSpeed > 0f
                ? new EgoFrame(Vector2.zero, forwardPlane).Direction(worldVelocity) / maxSpeed
                : Vector2.zero;
    }
}
