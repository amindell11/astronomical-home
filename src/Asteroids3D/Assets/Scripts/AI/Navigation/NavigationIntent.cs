using AI.Context;
using Movement.MPC;
using UnityEngine;

namespace AI.States
{
    /// <summary>Declarative description of what the navigator should do this frame, produced by AIState.Tick and consumed by Navigator.ApplyIntent.</summary>
    public struct NavigationIntent
    {
        public bool isValid;

        public GoalMode goalMode;
        public Vector2 goalPosition;
        public Vector2 goalVelocity;
        public float desiredRange;
        public float rangeTolerance;

        // Commanded world-plane velocity for GoalMode.VelocityReference (the tracker seam a learned goal-policy drives); ignored by the position-goal modes.
        public Vector2 velocityReference;

        // Commands a boost impulse this tick in VelocityReference mode; one-shot pacing is the chooser's job, the Booster's cooldown makes an unready command a no-op.
        public bool boost;

        // One snapshot of the ship we're engaging: consumed by the gunner's firing solution, tactical MPC costs (applyTacticalCosts), and obstacle exclusion (hasTarget).
        public bool hasTarget;
        public EnemyTarget target;
        public bool applyTacticalCosts;
        public float projectileSpeed;   // OUR weapon's projectile speed (intercept geometry)

        // MPC weight overrides (sparse; absent weight = base ×1)
        public WeightOverride[] weightOverrides;

        public bool enableFiring;

        public static NavigationIntent None => new NavigationIntent { isValid = false };
    }
}
