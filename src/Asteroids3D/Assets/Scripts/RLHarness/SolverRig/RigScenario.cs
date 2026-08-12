using Movement.MPC;
using Unity.Mathematics;

namespace Game.RLHarness
{
    /// <summary>One closed-loop solver-rig episode versus a stationary Dummy anchor: spawn geometry, the fixed anchored intent, and the metric window.</summary>
    public struct RigScenario
    {
        public float2 startPos;
        public float startYawRad;
        public float2 enemyPos;
        public float enemyYawRad;
        public AnchoredIntent intent;
        public float projectileSpeed;
        public float simDt;
        public float warmupSeconds;
        public float durationSeconds;

        /// <summary>Hold position at <paramref name="range"/> and face the anchor — the on-target station-keeping case where the yaw limit cycle lives. The on-target start is a fixed point (settle-capable selections hold it inertly); pass <paramref name="startFacingErrorDeg"/> to measure convergence instead.</summary>
        public static RigScenario VersusDummy(float range, float startFacingErrorDeg = 0f) => new()
        {
            startPos = default,
            startYawRad = math.radians(startFacingErrorDeg),
            enemyPos = new float2(0f, range),
            enemyYawRad = math.PI,
            intent = new AnchoredIntent
            {
                hasFacing = true,
                facingOffsetRad = 0f,
                facingWeight = 1f,
                hasVelocity = true,
                radialSpeed = 0f,
                tangentialSpeed = 0f,
                velocityWeight = 1f,
            },
            // Inert versus a stationary anchor (zero enemy velocity means the intercept anchor is pure LOS).
            projectileSpeed = 60f,
            simDt = 0.02f,
            warmupSeconds = 2f,
            durationSeconds = 20f,
        };
    }
}
