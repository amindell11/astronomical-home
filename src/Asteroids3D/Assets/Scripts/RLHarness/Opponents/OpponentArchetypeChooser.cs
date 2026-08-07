using AI;
using AI.Context;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Shared skeleton for the scripted opponent archetypes: live-target validity, the 5 Hz recompute cache, the border tangent-steer post-step, and the per-drive velocity pack (production world reference vs the K1-2 open-loop arms). Arena bounds enter once through Configure as plain floats.</summary>
    public abstract class OpponentArchetypeChooser : IIntentChooser, IScriptedVelocityReadout
    {
        /// <summary>The roster's 5 Hz decision cadence at the 0.02 s timestep — what every trained checkpoint and eval yardstick was measured against. Only the live pilot departs from it.</summary>
        public const int RosterRecomputeIntervalTicks = 10;

        protected Ship target;
        protected float speedFraction;
        private Vector2 arenaCenter;
        private float borderRadius;
        private ArchetypeDrive drive;
        private int recomputeIntervalTicks = RosterRecomputeIntervalTicks;

        private int tickCounter;
        private ActIntent cachedIntent = ActIntent.None;
        private int totalDecisions;
        private ScriptedVelocityCommand lastCommand;

        public ArchetypeDrive Drive => drive;
        public int TotalDecisions => totalDecisions;
        public ScriptedVelocityCommand LastCommand => lastCommand;
        public int RecomputeIntervalTicks => recomputeIntervalTicks;

        protected void Bind(Ship target, float speedFraction, Vector2 arenaCenter, float borderRadius,
            ArchetypeDrive drive, int recomputeIntervalTicks = RosterRecomputeIntervalTicks)
        {
            this.target = target;
            this.speedFraction = speedFraction;
            this.arenaCenter = arenaCenter;
            this.borderRadius = borderRadius;
            this.drive = drive;
            this.recomputeIntervalTicks = recomputeIntervalTicks;
            Reset();
        }

        public virtual void Reset()
        {
            tickCounter = 0;
            cachedIntent = ActIntent.None;
        }

        public ActIntent Decide(AIContext ctx, float dt)
        {
            if (!target || !target.gameObject.activeInHierarchy || ctx?.Self == null)
                return ActIntent.None;

            if (tickCounter % recomputeIntervalTicks == 0)
                cachedIntent = BuildIntent(ctx);
            tickCounter++;
            return cachedIntent;
        }

        protected abstract ActIntent BuildIntent(AIContext ctx);

        /// <summary>Emits the law's velocity through the bound drive and captures the readout (one bump per 5 Hz recompute). Production keeps the border-steered world reference byte-for-byte; the open-loop arms drop the steer, suppress fire (a hit would perturb the paired enemy path), and always target-tag the intent so both arms share one obstacle-exclusion state — the anchored arm packing the same law numbers into the enemy-polar channel.</summary>
        protected ActIntent Pack(ActIntent intent, Vector2 planePos, Vector2 lawVelocity)
        {
            switch (drive)
            {
                case ArchetypeDrive.Production:
                    intent.velocityReference = Steered(planePos, lawVelocity);
                    break;
                case ArchetypeDrive.OpenLoopLegacy:
                    intent.velocityReference = lawVelocity;
                    break;
                case ArchetypeDrive.OpenLoopAnchored:
                    intent.anchored = VelocityRebase.ToAnchored(lawVelocity, planePos, target.Kinematics.pos);
                    break;
            }
            if (drive != ArchetypeDrive.Production)
            {
                intent.enableFiring = false;
                if (!intent.hasTarget)
                {
                    intent.hasTarget = true;
                    intent.target = new EnemyTarget
                    {
                        kinematics = target.Kinematics,
                        dynamics = target.Dynamics,
                        source = target.transform,
                    };
                }
            }
            totalDecisions++;
            lastCommand = new ScriptedVelocityCommand(intent.velocityReference,
                intent.anchored.radialSpeed, intent.anchored.tangentialSpeed);
            return intent;
        }

        protected Vector2 Steered(Vector2 planePos, Vector2 commandedVel) =>
            ArchetypeSteering.BorderTangentSteer(planePos, commandedVel, arenaCenter, borderRadius,
                ArchetypeSteering.BorderMargin);
    }
}
