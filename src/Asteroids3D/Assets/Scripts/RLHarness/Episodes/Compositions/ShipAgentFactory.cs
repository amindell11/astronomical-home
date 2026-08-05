using System;
using AI;
using Ships;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.RLHarness
{
    // Boot boundaries resolve ModelAssets before composition.
    public static class ShipAgentFactory
    {
        public const string BehaviorName = ShipCombatPolicy.BehaviorName;
        public const string SmokeFixturePath = "Assets/Tests/Fixtures/ShipCombat-smoke.onnx";

        public static ShipAgent ComposeForTraining(EpisodePair pair, AgentChooser chooser,
            in RewardSpec spec, Vector2 arenaCenter, Transform parent = null) =>
            Compose(pair.Agent, pair.Baseline, chooser, in spec, arenaCenter,
                BehaviorType.Default, null, teamId: 0, parent);

        public static ShipAgent ComposeHeuristicOnly(EpisodePair pair, AgentChooser chooser,
            in RewardSpec spec, Vector2 arenaCenter, Transform parent = null) =>
            Compose(pair.Agent, pair.Baseline, chooser, in spec, arenaCenter,
                BehaviorType.HeuristicOnly, null, teamId: 0, parent);

        /// <summary>Held-out-seed eval path: pinned checkpoint, InferenceOnly, DeterministicInference (it defaults FALSE — InferenceOnly alone samples stochastically), pinned inference seed (Academy consumes it at the model runner's creation).</summary>
        public static ShipAgent ComposeInferenceOnly(EpisodePair pair, AgentChooser chooser,
            in RewardSpec spec, Vector2 arenaCenter, ModelAsset model, Transform parent = null)
        {
            Academy.Instance.InferenceSeed = EvalProtocol.InferenceSeed;
            return Compose(pair.Agent, pair.Baseline, chooser, in spec, arenaCenter,
                BehaviorType.InferenceOnly, model, teamId: 0, parent);
        }

        // Null models use the trainer; identical assets share one inference runner.
        public static (ShipAgent agentA, ShipAgent agentB) ComposeSelfPlayPair(EpisodePair pair,
            AgentChooser chooserA, AgentChooser chooserB, in RewardSpec spec, Vector2 arenaCenter,
            BehaviorType behaviorType, Transform parent = null, ModelAsset modelA = null,
            ModelAsset modelB = null)
        {
            if (!modelA != !modelB)
                throw new ArgumentException(
                    "Per-side checkpoints come in pairs: supply both ModelAssets or neither.");
            if (modelA)
                Academy.Instance.InferenceSeed = EvalProtocol.InferenceSeed;
            var agentA = Compose(pair.Agent, pair.Baseline, chooserA, in spec, arenaCenter,
                behaviorType, modelA, teamId: 0, parent);
            var agentB = Compose(pair.Baseline, pair.Agent, chooserB, in spec, arenaCenter,
                behaviorType, modelB, teamId: 1, parent);
            return (agentA, agentB);
        }

        private static ShipAgent Compose(Ship self, Ship opponent, AgentChooser chooser, in RewardSpec spec,
            Vector2 arenaCenter, BehaviorType behaviorType, ModelAsset model, int teamId, Transform parent)
        {
            var host = new GameObject("[ShipAgent]");
            if (parent) host.transform.SetParent(parent, false);
            host.SetActive(false);

            var behavior = host.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = BehaviorName;
            behavior.TeamId = teamId;
            behavior.BehaviorType = behaviorType;
            behavior.DeterministicInference = true;
            behavior.InferenceDevice = InferenceDevice.Burst;
            if (model) behavior.Model = model;

            // Asteroids ride an entity-attention buffer, not the flat vector; the Agent discovers it as a sensor on enable.
            var obstacleBuffer = host.AddComponent<BufferSensorComponent>();
            AgentObservations.ApplySchema(behavior, obstacleBuffer);

            var agent = host.AddComponent<ShipAgent>();
            var scout = ((AICommander)self.Commander).Scout;
            agent.Configure(self, opponent, chooser, in spec, arenaCenter, scout, obstacleBuffer);
            host.SetActive(true);
            return agent;
        }
    }
}
