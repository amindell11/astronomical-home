using AI.Context;
using Movement.MPC;
using UnityEngine;

namespace AI
{
    /// <summary>Declarative description of what the ship should do this frame, produced by an <see cref="IIntentChooser"/> and fanned out to Navigator.ApplyIntent and Gunner.ApplyIntent.</summary>
    public struct ActIntent
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

        // Commanded nose heading (world-plane yaw, radians, MPC convention fwd = (−sin, cos)), gated by hasFacing.
        public bool hasFacing;
        public float facingRad;

        // Enemy-anchored channels, re-resolved by the MPC every rollout step (see AnchoredIntent). Requires hasTarget; a targetless anchored intent collapses to the delegation priors.
        public AnchoredIntent anchored;

        // Manual trigger authority: the commander pushes primaryHeld to the weapon actuator and skips the Gunner.
        public bool manualFire;
        public bool primaryHeld;

        public static ActIntent None => new ActIntent { isValid = false };
    }
}
