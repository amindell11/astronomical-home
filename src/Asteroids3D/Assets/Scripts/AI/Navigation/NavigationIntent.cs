using Movement;
using Movement.MPC;
using UnityEngine;

namespace AI.States
{
    /// <summary>
    /// Declarative description of what the navigator should do this frame.
    /// Produced by AIState.Tick, consumed by Navigator.ApplyIntent.
    /// Replaces all Set*/Clear* mutation on Navigator.
    /// </summary>
    public struct NavigationIntent
    {
        public bool isValid;

        // Goal
        public GoalMode goalMode;
        public Vector2 goalPosition;
        public Vector2 goalVelocity;
        public float desiredRange;
        public float rangeTolerance;

        // Optional override from a high-level planner. When set, the MPC's position +
        // heading costs use this as the target instead of goalPosition. Tactical/range
        // costs still use goalPosition.
        public Vector2? navigationTarget;

        // Tactical
        public bool hasEnemy;
        public float enemyYawDeg;
        public float enemyYawRateDeg;
        public float projectileSpeed;
        public Dynamics enemyDynamics;
        public Transform obstacleExclusion;

        // MPC weight overrides (sparse; absent weight = base ×1)
        public WeightOverride[] weightOverrides;

        // Gunner
        public bool enableFiring;
        public Vector2 gunnerTarget;

        public static NavigationIntent None => new NavigationIntent { isValid = false };
    }
}
