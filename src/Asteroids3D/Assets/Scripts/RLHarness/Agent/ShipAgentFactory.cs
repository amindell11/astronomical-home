using System;
using AI;
using Ships;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Composes the ShipAgent GameObject with its BehaviorParameters fully configured before the Agent component enables (the policy captures them at first use). Keeps ML-Agents/InferenceEngine types out of the test assemblies via the mode-specific entry points.</summary>
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
            in RewardSpec spec, Vector2 arenaCenter, string onnxAssetPath, Transform parent = null)
        {
            var model = LoadModel(onnxAssetPath);
            Academy.Instance.InferenceSeed = EvalProtocol.InferenceSeed;
            return Compose(pair.Agent, pair.Baseline, chooser, in spec, arenaCenter,
                BehaviorType.InferenceOnly, model, teamId: 0, parent);
        }

        /// <summary>Both episode ships as parameter-shared agents: A on team 0 (self=Agent, primary/logged), B on team 1 (self=Baseline) — native ML-Agents self_play trains one policy against its own mirror.
        /// <paramref name="onnxAssetPath"/> null keeps the training path (the trainer supplies the policy); supplying it drives BOTH sides from one frozen checkpoint, which is the only way to observe a mirror match offline (capture/replay) — the training path never needs it because the trainer is attached.</summary>
        public static (ShipAgent agentA, ShipAgent agentB) ComposeSelfPlayPair(EpisodePair pair,
            AgentChooser chooserA, AgentChooser chooserB, in RewardSpec spec, Vector2 arenaCenter,
            BehaviorType behaviorType, Transform parent = null, string onnxAssetPath = null)
        {
            ModelAsset model = null;
            if (!string.IsNullOrEmpty(onnxAssetPath))
            {
                model = LoadModel(onnxAssetPath);
                Academy.Instance.InferenceSeed = EvalProtocol.InferenceSeed;
            }
            var agentA = Compose(pair.Agent, pair.Baseline, chooserA, in spec, arenaCenter,
                behaviorType, model, teamId: 0, parent);
            var agentB = Compose(pair.Baseline, pair.Agent, chooserB, in spec, arenaCenter,
                behaviorType, model, teamId: 1, parent);
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
            behavior.BrainParameters.VectorObservationSize = AgentObservations.CombatChannels;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(AgentActions.Count);
            behavior.DeterministicInference = true;
            behavior.InferenceDevice = InferenceDevice.Burst;
            if (model) behavior.Model = model;

            // Asteroids ride an entity-attention buffer, not the flat vector; the Agent discovers it as a sensor on enable.
            var obstacleBuffer = host.AddComponent<BufferSensorComponent>();
            obstacleBuffer.SensorName = "AsteroidBuffer";
            obstacleBuffer.ObservableSize = AgentObservations.ObstacleTokenFloats;
            obstacleBuffer.MaxNumObservables = AgentObservations.ObstacleTokenCap;

            var agent = host.AddComponent<ShipAgent>();
            var scout = ((AICommander)self.Commander).Scout;
            agent.Configure(self, opponent, chooser, in spec, arenaCenter, scout, obstacleBuffer);
            host.SetActive(true);
            return agent;
        }

        private static ModelAsset LoadModel(string assetPath)
        {
#if UNITY_EDITOR
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(assetPath);
            if (!model)
                throw new InvalidOperationException($"Failed to load ONNX model at {assetPath}.");
            return model;
#else
            throw new NotSupportedException("ShipAgentFactory loads checkpoints via AssetDatabase (editor only).");
#endif
        }
    }
}
