using System;
using AI;
using AI.Context;
using AI.States;
using Ships;
using Ships.Command;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Editor-authorable trained policy: self-hosts a <see cref="LivePilotAgent"/> in InferenceOnly mode on first Decide, targets the context's tracked enemy, and owns decision pacing (chooser-driven Academy stepping — see the Playable_RL_Pilot brief).</summary>
    [Serializable]
    public sealed class InferenceChooser : IIntentChooser
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
        private int ticksUntilDecision;
        private bool composeFailed;

        public InferenceChooser() { }

        internal InferenceChooser(ModelAsset model, float leashRadius)
        {
            this.model = model;
            this.leashRadius = leashRadius;
        }

        internal ModelAsset Model => model;
        internal LivePilotAgent Agent => agent;

        public NavigationIntent Decide(AIContext ctx, float dt)
        {
            if (!agent && !TryCompose(ctx))
                return NavigationIntent.None;

            Retarget(ctx.Combat.Enemy);

            if (enemy && enemy.gameObject.activeInHierarchy && --ticksUntilDecision <= 0)
            {
                agent.CaptureBoundary(self, enemy, leashCenter, leashRadius);
                agent.RequestDecision();
                Academy.Instance.EnvironmentStep();
                ticksUntilDecision = ShipCombatPolicy.DecisionIntervalSteps;
            }

            return mailbox.Decide(ctx, dt);
        }

        public void Reset()
        {
            mailbox.Reset();
            ticksUntilDecision = 0;
        }

        private void Retarget(Ship next)
        {
            if (next == enemy) return;
            enemy = next;
            ticksUntilDecision = 0;
            if (enemy)
                mailbox.Configure(enemy, self.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary));
            else
                mailbox.Reset();
        }

        private bool TryCompose(AIContext ctx)
        {
            if (composeFailed) return false;

            self = ctx.Self as Ship;
            if (!self || !model)
            {
                composeFailed = true;
                Debug.LogError(!model
                    ? "InferenceChooser has no ModelAsset assigned — pilot stays inert."
                    : "InferenceChooser needs a Ship context — pilot stays inert.", self);
                return false;
            }

            var host = new GameObject("[InferencePilot]");
            host.transform.SetParent(self.transform, false);
            host.SetActive(false);

            var behavior = host.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = ShipCombatPolicy.BehaviorName;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            behavior.BrainParameters.VectorObservationSize = AgentObservations.Size;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(AgentActions.Count);
            behavior.DeterministicInference = true;
            behavior.InferenceDevice = InferenceDevice.Burst;
            behavior.Model = model;

            agent = host.AddComponent<LivePilotAgent>();
            agent.Bind(mailbox);
            host.SetActive(true);

            Academy.Instance.AutomaticSteppingEnabled = false;
            leashCenter = self.Kinematics.pos;
            ticksUntilDecision = 0;
            return true;
        }
    }
}
