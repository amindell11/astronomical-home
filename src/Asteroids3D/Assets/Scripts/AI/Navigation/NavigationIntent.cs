using AI.Context;
using Movement.MPC;
using UnityEngine;

namespace AI.States
{
    /// <summary>Declarative description of what the navigator should do this frame, produced by an <see cref="IIntentChooser"/> and consumed by Navigator.ApplyIntent.</summary>
    public struct NavigationIntent
    {
        public bool isValid;

        public GoalMode goalMode;
        public Vector2 goalPosition;
        public Vector2 goalVelocity;
        public float desiredRange;
        public float rangeTolerance;

        // World-plane frame; read only in GoalMode.VelocityReference.
        public Vector2 velocityReference;

        // Boost impulse this tick, VelocityReference mode only; one-shot pacing and availability gating are the chooser's job (the Booster's cooldown backstops an unready command into a no-op).
        public bool boost;

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
