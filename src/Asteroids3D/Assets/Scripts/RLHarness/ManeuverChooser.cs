using AI;
using AI.Context;
using AI.States;
using Movement;
using Movement.MPC;
using UnityEngine;

namespace Game.RLHarness
{
    public enum ManeuverKind { Orbit, Break, Range }

    public class ManeuverChooser : IIntentChooser
    {
        private const int RecomputeIntervalTicks = 10; // 5 Hz at a 50 Hz fixed step
        private const float ProjectileSpeed = 100f;
        private const float RadialGain = 0.6f;
        private const float RangeGain = 0.6f;
        private const float RangeDamping = 0.5f;

        private DummyTarget dummy;
        private ManeuverKind kind;
        private float orbitRadius;
        private float desiredRange;
        private float orbitSpeedFraction = 0.9f;
        private bool freeYaw;
        private int orbitDirection = 1;

        private int tickCounter;
        private NavigationIntent cachedIntent = NavigationIntent.None;

        public void Configure(DummyTarget dummy, ManeuverKind kind, float orbitRadius, float desiredRange,
            float orbitSpeedFraction = 0.9f, bool freeYaw = false, int orbitDirection = 1)
        {
            this.dummy = dummy;
            this.kind = kind;
            this.orbitRadius = orbitRadius;
            this.desiredRange = desiredRange;
            this.orbitSpeedFraction = orbitSpeedFraction;
            this.freeYaw = freeYaw;
            this.orbitDirection = orbitDirection >= 0 ? 1 : -1;
            tickCounter = 0;
            cachedIntent = NavigationIntent.None;
        }

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (dummy == null || ctx?.Self == null) return NavigationIntent.None;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedIntent = BuildIntent(ctx);
            tickCounter++;
            return cachedIntent;
        }

        private NavigationIntent BuildIntent(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var maxSpeed = ctx.Self.Dynamics.maxSpeed;
            var p = self.pos;
            var d = dummy.PlanePosition;

            var los = d - p;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;

            var vRef = kind switch
            {
                ManeuverKind.Orbit => OrbitVelocity(losHat, r, maxSpeed),
                ManeuverKind.Break => BreakVelocity(losHat, maxSpeed),
                ManeuverKind.Range => RangeVelocity(losHat, r, self.vel, maxSpeed),
                _ => Vector2.zero,
            };

            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.VelocityReference,
                velocityReference = ClampSpeed(vRef, maxSpeed),
                hasTarget = true,
                target = BuildTarget(ctx, d),
                // Nose-on-target aim feeds SetEnemyState so intercept-yaw engages; freeYaw leaves the enemy state cleared so the solver yaws to exploit forward thrust. The tactical cost block stays off either way (goalMode-derived).
                applyTacticalCosts = !freeYaw,
                projectileSpeed = ProjectileSpeed,
            };
        }

        private Vector2 OrbitVelocity(Vector2 losHat, float r, float maxSpeed)
        {
            var tangent = orbitDirection * Perp(losHat);
            var outwardHat = -losHat;
            var radial = RadialGain * (orbitRadius - r) * outwardHat;
            return orbitSpeedFraction * maxSpeed * tangent + radial;
        }

        private Vector2 BreakVelocity(Vector2 losHat, float maxSpeed)
        {
            var dummyToShip = -losHat;
            var signedAim = Cross(dummyToShip, dummy.PlaneForward);
            var dir = signedAim > 0f ? -1f : 1f;
            return maxSpeed * dir * Perp(dummyToShip);
        }

        private Vector2 RangeVelocity(Vector2 losHat, float r, Vector2 selfVel, float maxSpeed)
        {
            var closing = RangeGain * (r - desiredRange) * losHat;
            var damping = RangeDamping * Vector2.Dot(selfVel, losHat) * losHat;
            return closing - damping;
        }

        private EnemyTarget BuildTarget(AIContext ctx, Vector2 dummyPlane)
        {
            var dummyForward = dummy.PlaneForward;
            var yawDeg = Mathf.Atan2(-dummyForward.x, dummyForward.y) * Mathf.Rad2Deg;
            return new EnemyTarget
            {
                kinematics = new Kinematics(dummyPlane, Vector2.zero, yawDeg, 0f, 0f),
                dynamics = ctx.Self.Dynamics,
                source = dummy.transform,
            };
        }

        private static Vector2 Perp(Vector2 v) => new(-v.y, v.x);
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 ClampSpeed(Vector2 v, float maxSpeed)
        {
            var m = v.magnitude;
            return m > maxSpeed && m > 1e-6f ? v * (maxSpeed / m) : v;
        }
    }
}
