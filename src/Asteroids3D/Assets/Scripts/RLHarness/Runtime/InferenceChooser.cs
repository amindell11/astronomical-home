using System;
using AI;
using AI.Context;
using Ships;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Editor-authorable trained policy: self-hosts a <see cref="LivePilotAgent"/> in InferenceOnly mode on first Decide, targets the context's tracked enemy, and paces boundary RequestDecision calls on the Academy auto-clock.</summary>
    [Serializable]
    public sealed class InferenceChooser : IIntentChooser, IPolicyReadout
    {
        [Tooltip("Trained ShipCombat checkpoint (ONNX).")]
        [SerializeField] private ModelAsset model;

        [Tooltip("Radius of the border observation's leash, centered on the compose-time position; the trained policy avoids the edge. Author a huge value for no leash. 120 matches training.")]
        [SerializeField] private float leashRadius = 120f;

        private readonly AgentChooser mailbox = new();
        private LivePilotAgent agent;
        private Ship self;
        private Ship enemy;
        private Vector2 leashCenter;
        private bool reanchorLeash;
        private int ticksUntilDecision;

        public InferenceChooser() { }

        internal InferenceChooser(ModelAsset model, float leashRadius)
        {
            this.model = model;
            this.leashRadius = leashRadius;
        }

        internal ModelAsset Model => model;
        internal LivePilotAgent Agent => agent;

        public int Count => mailbox.Count;
        public int TotalDecisions => mailbox.TotalDecisions;
        public PolicyAction ActionFromNewest(int index) => mailbox.ActionFromNewest(index);

        public BrainDecision? Decide(AIContext ctx, float dt)
        {
            if (!agent)
                Compose(ctx);

            // Deferred to the first post-reset tick: Reset fires before a respawn teleport lands.
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

            return mailbox.Decide(ctx, dt);
        }

        public void Reset()
        {
            mailbox.Reset();
            ticksUntilDecision = 0;
            reanchorLeash = true;
        }

        private void Retarget(Ship next)
        {
            if (next == enemy) return;
            enemy = next;
            ticksUntilDecision = 0;
            if (enemy)
                mailbox.Configure(enemy);
            else
                mailbox.Reset();
        }

        private void Compose(AIContext ctx)
        {
            self = ctx.Self as Ship;
            if (!self)
                throw new InvalidOperationException("InferenceChooser requires a Ship context.");
            if (!model)
                throw new InvalidOperationException($"InferenceChooser on '{self.name}' has no ModelAsset assigned.");

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
            agent.Bind(mailbox, ctx.Scout, obstacleBuffer);
            host.SetActive(true);

            leashCenter = self.Kinematics.pos;
            ticksUntilDecision = 0;
        }
    }
}
