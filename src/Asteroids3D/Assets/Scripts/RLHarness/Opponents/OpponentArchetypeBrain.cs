using AI;
using AI.Context;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Shared skeleton for the scripted opponent archetypes: live-target validity, the 5 Hz recompute cache, the border tangent-steer post-step, and the per-drive velocity pack (production world reference vs the K1-2 open-loop arms). Arena bounds enter once through Configure as plain floats.</summary>
    public abstract class OpponentArchetypeBrain : Brain, IScriptedVelocityReadout
    {
        protected const int RecomputeIntervalTicks = 10;

        protected Ship target;
        protected float speedFraction;
        private Vector2 arenaCenter;
        private float borderRadius;
        private ArchetypeDrive drive;

        private int tickCounter;
        private BrainDecision? cachedDecision;
        private int totalDecisions;
        private ScriptedVelocityCommand lastCommand;

        public ArchetypeDrive Drive => drive;
        public int TotalDecisions => totalDecisions;
        public ScriptedVelocityCommand LastCommand => lastCommand;

        protected void Bind(Ship target, float speedFraction, Vector2 arenaCenter, float borderRadius,
            ArchetypeDrive drive)
        {
            this.target = target;
            this.speedFraction = speedFraction;
            this.arenaCenter = arenaCenter;
            this.borderRadius = borderRadius;
            this.drive = drive;
            ResetState();
        }

        public override void ResetState()
        {
            tickCounter = 0;
            cachedDecision = null;
        }

        public override BrainDecision? Decide(AIContext ctx)
        {
            if (!target || !target.gameObject.activeInHierarchy || ctx?.Self == null)
                return null;

            if (tickCounter % RecomputeIntervalTicks == 0)
                cachedDecision = BuildDecision(ctx);
            tickCounter++;
            return cachedDecision;
        }

        protected abstract BrainDecision BuildDecision(AIContext ctx);

        /// <summary>Emits the law's velocity through the bound drive and captures the readout (one bump per 5 Hz recompute). Production keeps the border-steered world reference byte-for-byte; the open-loop arms drop the steer and suppress fire (a hit would perturb the paired enemy path) while keeping the aim, the anchored arm packing the same law numbers into the enemy-polar channel.</summary>
        protected BrainDecision Pack(Vector2 planePos, Vector2 lawVelocity, bool engages)
        {
            var builder = NavObjective.Anchored(new EnemyTarget
            {
                kinematics = target.Kinematics,
                dynamics = target.Dynamics,
            });

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
                    ? Steered(planePos, lawVelocity)
                    : lawVelocity;
                builder = builder.Planar(worldVelocity);
            }

            if (engages) builder = builder.Facing(0f, 1f);

            var fire = engages && drive == ArchetypeDrive.Production ? FireControl.Auto : FireControl.Hold;
            totalDecisions++;
            lastCommand = new ScriptedVelocityCommand(worldVelocity, radialSpeed, tangentialSpeed);
            return new BrainDecision(builder, fire, fire);
        }

        protected Vector2 Steered(Vector2 planePos, Vector2 commandedVel) =>
            ArchetypeSteering.BorderTangentSteer(planePos, commandedVel, arenaCenter, borderRadius,
                ArchetypeSteering.BorderMargin);
    }
}
