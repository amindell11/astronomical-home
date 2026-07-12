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

        // One snapshot of the ship we're engaging: the gunner reads its kinematics for the firing solution; the navigator uses it for tactical MPC costs (when applyTacticalCosts) and obstacle exclusion (whenever hasTarget).
        public bool hasTarget;
        public EnemyTarget target;
        public bool applyTacticalCosts;
        public float projectileSpeed;   // OUR weapon's projectile speed (intercept geometry)

        // MPC weight overrides (sparse; absent weight = base ×1)
        public WeightOverride[] weightOverrides;

        // Gunner — fire at the target; the Gunner resolves its own firing solution.
        public bool enableFiring;

        public static NavigationIntent None => new NavigationIntent { isValid = false };
    }
}
