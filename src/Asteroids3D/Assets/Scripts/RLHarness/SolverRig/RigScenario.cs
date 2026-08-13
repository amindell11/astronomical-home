using Movement.MPC;
using Unity.Mathematics;

namespace Game.RLHarness
{
    /// <summary>Scripted referent motion for one rig slot — a stimulus, not an opponent AI. Static/ConstantVelocity/Orbit are closed-form in episode time; Pursue integrates constant-speed pure pursuit of the live ship, still deterministic inside the closed loop.</summary>
    public enum RigLawKind
    {
        None = 0,
        Static,
        ConstantVelocity,
        Orbit,
        Pursue,
    }

    /// <summary>One referent's scripted law. Moving laws face their velocity; Static faces <see cref="yaw"/> (MPC convention, fwd = (-sin, cos)).</summary>
    public struct RigLaw
    {
        public RigLawKind kind;
        public float2 p0;         // Static/ConstantVelocity/Pursue start; Orbit center
        public float2 v0;         // ConstantVelocity velocity
        public float yaw;         // Static facing
        public float radius;      // Orbit ring radius
        public float angularRate; // Orbit rad/s, CCW positive
        public float phase;       // Orbit start angle, CCW from +x

        public static RigLaw Static(float2 pos, float yaw = 0f) =>
            new() { kind = RigLawKind.Static, p0 = pos, yaw = yaw };

        public static RigLaw ConstantVelocity(float2 start, float2 vel) =>
            new() { kind = RigLawKind.ConstantVelocity, p0 = start, v0 = vel };

        public static RigLaw Orbit(float2 center, float radius, float angularRate, float phase = 0f) =>
            new() { kind = RigLawKind.Orbit, p0 = center, radius = radius, angularRate = angularRate, phase = phase };

        /// <summary>Constant-speed pure pursuit of the live ship; speed rides in <see cref="v0"/>.x.</summary>
        public static RigLaw Pursue(float2 start, float speed) =>
            new() { kind = RigLawKind.Pursue, p0 = start, v0 = new float2(speed, 0f) };
    }

    /// <summary>One synthetic obstacle circle, authored in plane space; the rig feeds it through the production ConvertObstacles path as a colliderless <see cref="AI.Scanning.DetectedObstacle"/>.</summary>
    public struct RigCircle
    {
        public float2 center;
        public float radius;

        public RigCircle(float2 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }
    }

    /// <summary>One closed-loop solver-rig episode: spawn geometry, scripted referent laws (enemy = referent 0; synthetic 1–2), the fixed intent sentence, synthetic obstacles, and the metric window.</summary>
    public struct RigScenario
    {
        public float2 startPos;
        public float startYawRad;
        public RigLaw enemyLaw;     // kind None = no hostile: referent-0 slots drop, no enemy rollout
        public RigLaw referent1Law;
        public RigLaw referent2Law;
        public IntentSentence intent;
        public RigCircle[] obstacles;
        public float posWidthOverride; // > 0 runs on an in-memory MpcSettings clone; the asset is never written
        public float projectileSpeed;
        public float simDt;
        public float warmupSeconds;
        public float durationSeconds;

        /// <summary>Hold position at <paramref name="range"/> and face the anchor — the on-target station-keeping case where the yaw limit cycle lives. The on-target start is a fixed point (settle-capable selections hold it inertly); pass <paramref name="startFacingErrorDeg"/> to measure convergence instead.</summary>
        public static RigScenario VersusDummy(float range, float startFacingErrorDeg = 0f) => new()
        {
            startPos = default,
            startYawRad = math.radians(startFacingErrorDeg),
            enemyLaw = RigLaw.Static(new float2(0f, range), math.PI),
            intent = new IntentSentence
            {
                aim = new AimSlot { armed = true, offsetRad = 0f, weight = 1f },
                vel = new VelSlot { armed = true, radialSpeed = 0f, tangentialSpeed = 0f, weight = 1f },
            },
            // Inert versus a stationary anchor (zero enemy velocity means the intercept anchor is pure LOS).
            projectileSpeed = 60f,
            simDt = 0.02f,
            warmupSeconds = 2f,
            durationSeconds = 20f,
        };
    }
}
