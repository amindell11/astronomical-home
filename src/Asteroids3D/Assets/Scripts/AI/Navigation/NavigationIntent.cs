using AI.Context;
using Movement.MPC;
using UnityEngine;

namespace AI.States
{
    /// <summary>Declarative description of what the navigator should do this frame, produced by an <see cref="IIntentChooser"/> and consumed by Navigator.ApplyIntent.</summary>
    public struct NavigationIntent
    {
        public bool isValid;

        // World-plane frame.
        public Vector2 velocityReference;

        // Boost impulse this tick; one-shot pacing and availability gating are the chooser's job (the Booster's cooldown backstops an unready command into a no-op).
        public bool boost;

        public bool hasTarget;
        public EnemyTarget target;
        // Routes the target into the solver's intercept-facing geometry (aim), independent of firing.
        public bool aimAtTarget;
        public float projectileSpeed;   // OUR weapon's projectile speed (intercept geometry)

        // MPC weight overrides (sparse; absent weight = base ×1)
        public WeightOverride[] weightOverrides;

        public bool enableFiring;

        public static NavigationIntent None => new NavigationIntent { isValid = false };
    }
}
