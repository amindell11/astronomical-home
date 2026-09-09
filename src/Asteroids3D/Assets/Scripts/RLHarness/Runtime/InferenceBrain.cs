using System;
using AI;
using AI.Context;
using Ships;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using AI.Strategy;

namespace Game.RLHarness
{
    /// <summary>Editor-authorable trained policy: a <see cref="PolicyBrain"/> that fills its own mailbox — it self-hosts a <see cref="LivePilotAgent"/> in InferenceOnly mode on first Decide, targets the context's tracked enemy, and paces boundary RequestDecision calls on the Academy auto-clock.</summary>
    public sealed class InferenceBrain : PolicyBrain
    {
        [Tooltip("Trained ShipCombat checkpoint (ONNX).")]
        [SerializeField] private ModelAsset model;

        [Tooltip("Radius of the border observation's leash, centered on the compose-time position; the trained policy avoids the edge. Author a huge value for no leash. 120 matches training.")]
        [SerializeField] private float leashRadius = 120f;

        private LivePilotAgent agent;
        private Ship self;
        private Ship enemy;
        private Vector2 leashCenter;
        private bool reanchorLeash;
        private int ticksUntilDecision;

        internal ModelAsset Model => model;
        internal LivePilotAgent Agent => agent;

        /// <summary>Authoring seam for runtime installs; the prefab path serializes these two directly.</summary>
        internal void ConfigureModel(ModelAsset model, float leashRadius)
        {
            this.model = model;
            this.leashRadius = leashRadius;
        }

        public override BrainDecision? Decide(AIContext ctx)
        {
            if (!agent)
                Compose(ctx);

            // Deferred to the first post-reset tick: ResetState fires before a respawn teleport lands.
            if (reanchorLeash)
            {
                leashCenter = self.Kinematics.pos;
                reanchorLeash = false;
            }

            Retarget(ctx.Combat.Enemy);

            if (enemy && enemy.gameObject.activeInHierarchy && --ticksUntilDecision <= 0)
            {
                agent.CaptureBoundary(self, enemy, leashCenter, leashRadius);
                agent.RequestDecision();
                ticksUntilDecision = ShipCombatPolicy.DecisionIntervalSteps;
            }

            return base.Decide(ctx);
        }

        public override void ResetState()
        {
            base.ResetState();
            ticksUntilDecision = 0;
            reanchorLeash = true;
        }

        private void Retarget(Ship next)
        {
            if (next == enemy) return;
            enemy = next;
            ticksUntilDecision = 0;
            // Mailbox-only: a retarget must not re-anchor the leash, which ResetState would.
            if (enemy)
                Configure(enemy);
            else
                ResetMailbox();
        }

        private void Compose(AIContext ctx)
        {
            self = ctx.Self as Ship;
            if (!self)
                throw new InvalidOperationException("InferenceBrain requires a Ship context.");
            if (!model)
                throw new InvalidOperationException($"InferenceBrain on '{self.name}' has no ModelAsset assigned.");

            var host = new GameObject("[InferencePilot]");
            host.transform.SetParent(self.transform, false);
            host.SetActive(false);

            var behavior = host.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = ShipCombatPolicy.BehaviorName;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            behavior.DeterministicInference = true;
            behavior.InferenceDevice = InferenceDevice.Burst;
            behavior.Model = model;

            // Mirrors ShipAgentFactory's composition — asteroids ride an entity-attention buffer, not the flat vector.
            var obstacleBuffer = host.AddComponent<BufferSensorComponent>();
            AgentObservations.ApplySchema(behavior, obstacleBuffer);

            agent = host.AddComponent<LivePilotAgent>();
            agent.Bind(this, ctx.Scout, obstacleBuffer,
                ((AICommander)self.Commander).Navigator.mpcSettings.speedRef);
            host.SetActive(true);

            leashCenter = self.Kinematics.pos;
            ticksUntilDecision = 0;
        }
    }
}
