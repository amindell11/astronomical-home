using System;
using AI;
using AI.Context;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The one scripted opponent component, switched on <see cref="OpponentArchetype"/>: target validity, the 5 Hz recompute cache, the border tangent-steer post-step, and the per-drive velocity pack (production world reference vs the K1-2 open-loop arms). The roster Configures a pinned target per episode draw; an authored pilot serializes the same fields and can instead track the context's enemy.</summary>
    public sealed class ArchetypeBrain : Brain, IScriptedVelocityReadout
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
        private ArchetypeDrive drive;

        private int tickCounter;
        private BrainDecision? cachedDecision;
        private int totalDecisions;
        private ScriptedVelocityCommand lastCommand;

        private System.Random rng;
        private int jukeSign = 1;
        private int recomputes;
        private int jukeEveryRecomputes = 1;

        public ArchetypeDrive Drive => drive;
        public int TotalDecisions => totalDecisions;
        public ScriptedVelocityCommand LastCommand => lastCommand;

        public void Configure(Ship target, OpponentArchetype archetype, in OpponentDraw shape, int jukeSeed,
            Vector2 arenaCenter, float borderRadius, ArchetypeDrive drive = ArchetypeDrive.Production)
        {
            this.target = target;
            this.archetype = archetype;
            this.shape = shape;
            this.jukeSeed = jukeSeed;
            this.arenaCenter = arenaCenter;
            this.borderRadius = borderRadius;
            this.drive = drive;
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

            if (!borderAnchored)
            {
                // An authored pilot has no Configure call; its border circle centers where it first decides.
                arenaCenter = ctx.Self.Kinematics.pos;
                borderAnchored = true;
            }

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

        /// <summary>Emits the law's velocity through the bound drive and captures the readout (one bump per 5 Hz recompute). Production keeps the border-steered world reference byte-for-byte; the open-loop arms drop the steer and suppress fire (a hit would perturb the paired enemy path) while keeping the aim, the anchored arm packing the same law numbers into the enemy-polar channel.</summary>
        internal BrainDecision Pack(Vector2 planePos, Vector2 lawVelocity, bool engages)
        {
            var builder = NavObjective.Anchored(target.Id);

            var worldVelocity = Vector2.zero;
            float radialSpeed = 0f, tangentialSpeed = 0f;

            if (drive == ArchetypeDrive.OpenLoopAnchored)
            {
                var polar = VelocityRebase.ToAnchored(lawVelocity, planePos, target.Kinematics.pos);
                radialSpeed = polar.radialSpeed;
                tangentialSpeed = polar.tangentialSpeed;
                builder = builder.Velocity(radialSpeed, tangentialSpeed, polar.velocityWeight);
            }
            else
            {
                worldVelocity = drive == ArchetypeDrive.Production
                    ? ArchetypeSteering.BorderTangentSteer(planePos, lawVelocity, arenaCenter, borderRadius,
                        ArchetypeSteering.BorderMargin)
                    : lawVelocity;
                builder = builder.Planar(worldVelocity);
            }

            if (engages) builder = builder.Facing(0f, 1f);

            var fire = engages && drive == ArchetypeDrive.Production ? FireControl.Auto : FireControl.Hold;
            totalDecisions++;
            lastCommand = new ScriptedVelocityCommand(worldVelocity, radialSpeed, tangentialSpeed);
            return new BrainDecision(builder, fire, fire);
        }
    }
}
