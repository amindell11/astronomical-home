using AI;
using AI.Context;
using AI.States;
using Movement;
using Movement.MPC;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Deterministic border handling for the scripted opponent archetypes, as one pure velocity-law post-step (the <see cref="RangerChooser.HoldRangeVelocity"/> style).</summary>
    public static class ArchetypeSteering
    {
        // Full tangent at half depth: at maxSpeed 25 with ~18 u/s² braking, radial overshoot past the tangent point is ~17 u, so it must sit well inside the border.
        public const float BorderMargin = 60f;

        /// <summary>Inside the edge margin, rotates an outward-bound commanded velocity toward the border tangent — full tangent at half the margin depth, bending on toward inward by the border itself (momentum headroom) — preserving speed; inward-bound commands pass through.</summary>
        public static Vector2 BorderTangentSteer(Vector2 planePos, Vector2 commandedVel,
            Vector2 arenaCenter, float borderRadius, float margin)
        {
            var radial = planePos - arenaCenter;
            var r = radial.magnitude;
            var inner = borderRadius - margin;
            var speed = commandedVel.magnitude;
            if (r <= inner || r < 1e-4f || speed < 1e-4f) return commandedVel;

            var outwardHat = radial / r;
            if (Vector2.Dot(commandedVel, outwardHat) <= 0f) return commandedVel;

            var perp = new Vector2(-outwardHat.y, outwardHat.x);
            var tangentHat = Vector2.Dot(commandedVel, perp) >= 0f ? perp : -perp;
            var t = (r - inner) / (0.5f * margin);
            var dir = t <= 1f
                ? Vector2.Lerp(commandedVel / speed, tangentHat, t)
                : Vector2.Lerp(tangentHat, -outwardHat, Mathf.Clamp01(t - 1f));
            return speed * dir.normalized;
        }
    }

    /// <summary>Shared skeleton for the scripted opponent archetypes: live-target validity, the 5 Hz recompute cache, and the border tangent-steer post-step. Arena bounds enter once through Configure as plain floats.</summary>
    public abstract class OpponentArchetypeChooser : IIntentChooser
    {
        protected const int RecomputeIntervalTicks = 10;

        protected Ship target;
        protected float speedFraction;
        private Vector2 arenaCenter;
        private float borderRadius;

        private int tickCounter;
        private NavigationIntent cachedIntent = NavigationIntent.None;

        protected void Bind(Ship target, float speedFraction, Vector2 arenaCenter, float borderRadius)
        {
            this.target = target;
            this.speedFraction = speedFraction;
            this.arenaCenter = arenaCenter;
            this.borderRadius = borderRadius;
            Reset();
        }

        public virtual void Reset()
        {
            tickCounter = 0;
            cachedIntent = NavigationIntent.None;
        }

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (!target || !target.gameObject.activeInHierarchy || ctx?.Self == null)
                return NavigationIntent.None;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedIntent = BuildIntent(ctx);
            tickCounter++;
            return cachedIntent;
        }

        protected abstract NavigationIntent BuildIntent(AIContext ctx);

        protected Vector2 Steered(Vector2 planePos, Vector2 commandedVel) =>
            ArchetypeSteering.BorderTangentSteer(planePos, commandedVel, arenaCenter, borderRadius,
                ArchetypeSteering.BorderMargin);
    }

    /// <summary>The pursuit teacher: flees along the threat LOS with a seeded tangential juke that flips at a jittered cadence; never fires.</summary>
    public class EvaderChooser : OpponentArchetypeChooser
    {
        private const float JukeBlend = 0.6f;

        private float jukePeriodSeconds;
        private int jukeSeed;

        private System.Random rng;
        private int jukeSign = 1;
        private int recomputes;
        private int jukeEveryRecomputes = 1;

        public void Configure(Ship threat, float speedFraction, float jukePeriodSeconds, int jukeSeed,
            Vector2 arenaCenter, float borderRadius)
        {
            this.jukePeriodSeconds = jukePeriodSeconds;
            this.jukeSeed = jukeSeed;
            Bind(threat, speedFraction, arenaCenter, borderRadius);
        }

        public override void Reset()
        {
            base.Reset();
            rng = new System.Random(jukeSeed);
            jukeSign = 1;
            recomputes = 0;
            jukeEveryRecomputes = Mathf.Max(1, Mathf.RoundToInt(
                jukePeriodSeconds / (RecomputeIntervalTicks * Time.fixedDeltaTime)));
        }

        protected override NavigationIntent BuildIntent(AIContext ctx)
        {
            if (recomputes++ % jukeEveryRecomputes == 0)
                jukeSign = rng.Next(2) == 0 ? -1 : 1;

            var self = ctx.Self.Kinematics;
            var away = self.pos - target.Kinematics.pos;
            var fleeHat = away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.up;
            var dir = (fleeHat + JukeBlend * jukeSign * new Vector2(-fleeHat.y, fleeHat.x)).normalized;

            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.VelocityReference,
                velocityReference = Steered(self.pos, speedFraction * ctx.Self.Dynamics.maxSpeed * dir),
            };
        }
    }

    /// <summary>Circles the live target at a jittered radius (<see cref="ManeuverChooser"/>'s orbit law generalized to a moving center), firing from inside the envelope.</summary>
    public class OrbiterChooser : OpponentArchetypeChooser
    {
        private const float RadialGain = 0.6f;

        private float orbitRadius;
        private int orbitDirection = 1;
        private float projectileSpeed;

        public void Configure(Ship target, float orbitRadius, int orbitDirection, float speedFraction,
            float projectileSpeed, Vector2 arenaCenter, float borderRadius)
        {
            this.orbitRadius = orbitRadius;
            this.orbitDirection = orbitDirection >= 0 ? 1 : -1;
            this.projectileSpeed = projectileSpeed;
            Bind(target, speedFraction, arenaCenter, borderRadius);
        }

        protected override NavigationIntent BuildIntent(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var enemy = target.Kinematics;
            var maxSpeed = ctx.Self.Dynamics.maxSpeed;

            var los = enemy.pos - self.pos;
            var r = los.magnitude;
            var losHat = r > 1e-4f ? los / r : Vector2.up;
            var tangent = orbitDirection * new Vector2(-losHat.y, losHat.x);
            var radial = RadialGain * (orbitRadius - r) * -losHat;
            var vRef = Vector2.ClampMagnitude(speedFraction * maxSpeed * tangent + radial, maxSpeed);

            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.VelocityReference,
                velocityReference = Steered(self.pos, vRef),
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = enemy,
                    dynamics = target.Dynamics,
                    source = target.transform,
                },
                // Solely to route SetEnemyState for intercept-yaw aim; the tactical cost block stays off (goalMode-derived tacticalEnabled).
                applyTacticalCosts = true,
                projectileSpeed = projectileSpeed,
                enableFiring = true,
            };
        }
    }

    /// <summary>The anti-exploit flavor: holds a jittered long range on the LOS (<see cref="RangerChooser.HoldRangeVelocity"/> capped to the jittered speed) and fires from the envelope edge.</summary>
    public class KiterChooser : OpponentArchetypeChooser
    {
        private float desiredRange;
        private float projectileSpeed;

        public void Configure(Ship target, float desiredRange, float speedFraction, float projectileSpeed,
            Vector2 arenaCenter, float borderRadius)
        {
            this.desiredRange = desiredRange;
            this.projectileSpeed = projectileSpeed;
            Bind(target, speedFraction, arenaCenter, borderRadius);
        }

        protected override NavigationIntent BuildIntent(AIContext ctx)
        {
            var self = ctx.Self.Kinematics;
            var enemy = target.Kinematics;
            var vRef = RangerChooser.HoldRangeVelocity(in self, in enemy, desiredRange,
                speedFraction * ctx.Self.Dynamics.maxSpeed);

            return new NavigationIntent
            {
                isValid = true,
                goalMode = GoalMode.VelocityReference,
                velocityReference = Steered(self.pos, vRef),
                hasTarget = true,
                target = new EnemyTarget
                {
                    kinematics = enemy,
                    dynamics = target.Dynamics,
                    source = target.transform,
                },
                applyTacticalCosts = true,
                projectileSpeed = projectileSpeed,
                enableFiring = true,
            };
        }
    }

    /// <summary>The curriculum floor: a killable airframe pinned to a zero-velocity reference — no motion goal, no aim, no fire.</summary>
    public class DummyChooser : IIntentChooser
    {
        public NavigationIntent Decide(AIContext ctx, float dt) =>
            ctx?.Self == null
                ? NavigationIntent.None
                : new NavigationIntent
                {
                    isValid = true,
                    goalMode = GoalMode.VelocityReference,
                    velocityReference = Vector2.zero,
                };
    }
}
