using System;
using AI;
using AI.Context;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The one scripted opponent component, switched on <see cref="OpponentArchetype"/>: target validity, the 5 Hz recompute cache, and the border tangent-steer world-velocity pack. The roster Configures a pinned target per episode draw; an authored pilot serializes the same fields and can instead track the context's enemy.</summary>
    public sealed class ArchetypeBrain : Brain
    {
        private const int RecomputeIntervalTicks = 10;

        [Tooltip("Which archetype to fly. Aggressor and Kiter hold a range and fire, Orbiter circles and fires, Evader flees and never fires, Dummy sits still.")]
        [SerializeField] private OpponentArchetype archetype = OpponentArchetype.Aggressor;

        [SerializeField] private OpponentDraw shape = new()
        {
            speedFraction = 0.85f,
            jukePeriod = 1.2f,
            orbitRadius = 16f,
            orbitDirection = 1,
            desiredRange = 10f,
        };

        [Tooltip("Evader: the seed its juke flip sequence runs on.")]
        [SerializeField] private int jukeSeed = 1;

        [Tooltip("Radius of the border circle the archetype tangent-steers off, centered where the brain first decides. Author a huge value for no border.")]
        [SerializeField] private float borderRadius = 500f;

        [Tooltip("Track the context's enemy, resetting brain state whenever it changes; off flies the Configure-pinned target — the roster/trained behavior.")]
        [SerializeField] private bool liveTargeting;

        private Ship target;
        private Vector2 arenaCenter;
        private bool borderAnchored;

        private int tickCounter;
        private BrainDecision? cachedDecision;

        private System.Random rng;
        private int jukeSign = 1;
        private int recomputes;
        private int jukeEveryRecomputes = 1;

        public void Configure(Ship target, OpponentArchetype archetype, in OpponentDraw shape, int jukeSeed,
            Vector2 arenaCenter, float borderRadius)
        {
            this.target = target;
            this.archetype = archetype;
            this.shape = shape;
            this.jukeSeed = jukeSeed;
            this.arenaCenter = arenaCenter;
            this.borderRadius = borderRadius;
            borderAnchored = true;
            ResetState();
        }

        public override void ResetState()
        {
            tickCounter = 0;
            cachedDecision = null;
            rng = new System.Random(jukeSeed);
            jukeSign = 1;
            recomputes = 0;
            jukeEveryRecomputes = Mathf.Max(1, Mathf.RoundToInt(
                shape.jukePeriod / (RecomputeIntervalTicks * Time.fixedDeltaTime)));
        }

        public override BrainDecision? Decide(AIContext ctx)
        {
            if (ctx?.Self == null) return null;

            if (!borderAnchored)
            {
                // No Configure call on authored pilots: the border centers at first Decide, target or not.
                arenaCenter = ctx.Self.Kinematics.pos;
                borderAnchored = true;
            }

            if (liveTargeting && ctx.Combat.Enemy != target)
            {
                target = ctx.Combat.Enemy;
                ResetState();
            }

            if (archetype != OpponentArchetype.Dummy
                && (!target || !target.gameObject.activeInHierarchy))
                return null;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedDecision = BuildDecision(ctx);
            tickCounter++;
            return cachedDecision;
        }

        private BrainDecision BuildDecision(AIContext ctx)
        {
            if (archetype == OpponentArchetype.Dummy)
                return new BrainDecision(NavObjective.Planar(Vector2.zero));

            var self = ctx.Self.Kinematics;
            var maxSpeed = ctx.Self.Dynamics.maxSpeed;
            switch (archetype)
            {
                case OpponentArchetype.Aggressor:
                case OpponentArchetype.Kiter:
                    return Pack(self.pos, RangerBrain.HoldRangeVelocity(in self, target.Kinematics,
                        shape.desiredRange, shape.speedFraction * maxSpeed), engages: true);
                case OpponentArchetype.Evader:
                    if (recomputes++ % jukeEveryRecomputes == 0)
                        jukeSign = rng.Next(2) == 0 ? -1 : 1;
                    return Pack(self.pos, ArchetypeLaws.FleeVelocity(self.pos, target.Kinematics.pos,
                        jukeSign, shape.speedFraction * maxSpeed), engages: false);
                case OpponentArchetype.Orbiter:
                    return Pack(self.pos, ArchetypeLaws.OrbitVelocity(in self, target.Kinematics,
                        shape.orbitRadius, shape.orbitDirection >= 0 ? 1 : -1, shape.speedFraction, maxSpeed),
                        engages: true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype), archetype, null);
            }
        }

        /// <summary>Packs the law's velocity as the border-steered world reference and, for the fire-capable archetypes, the enemy-facing aim.</summary>
        private BrainDecision Pack(Vector2 planePos, Vector2 lawVelocity, bool engages)
        {
            var worldVelocity = ArchetypeSteering.BorderTangentSteer(planePos, lawVelocity, arenaCenter, borderRadius,
                ArchetypeSteering.BorderMargin);
            var builder = NavObjective.Anchored(target.Id).Planar(worldVelocity);
            if (engages) builder = builder.Facing(0f, 1f);
            return new BrainDecision(builder, engages, engages);
        }
    }
}
