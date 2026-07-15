using System;
using Unity.InferenceEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Composes the ShipAgent GameObject with its BehaviorParameters fully configured before the Agent component enables (the policy captures them at first use). Keeps ML-Agents/InferenceEngine types out of the test assemblies via the mode-specific entry points.</summary>
    public static class ShipAgentFactory
    {
        public const string BehaviorName = "ShipCombat";

        public static ShipAgent ComposeForTraining(EpisodePair pair, AgentChooser chooser,
            in RewardSpec spec, Vector2 arenaCenter, Transform parent = null) =>
            Compose(pair, chooser, in spec, arenaCenter, BehaviorType.Default, null, parent);

        public static ShipAgent ComposeHeuristicOnly(EpisodePair pair, AgentChooser chooser,
            in RewardSpec spec, Vector2 arenaCenter, Transform parent = null) =>
            Compose(pair, chooser, in spec, arenaCenter, BehaviorType.HeuristicOnly, null, parent);

        /// <summary>Held-out-seed eval path: pinned checkpoint, InferenceOnly, DeterministicInference (it defaults FALSE — InferenceOnly alone samples stochastically).</summary>
        public static ShipAgent ComposeInferenceOnly(EpisodePair pair, AgentChooser chooser,
            in RewardSpec spec, Vector2 arenaCenter, string onnxAssetPath, Transform parent = null)
        {
            var model = LoadModel(onnxAssetPath);
            return Compose(pair, chooser, in spec, arenaCenter, BehaviorType.InferenceOnly, model, parent);
        }

        private static ShipAgent Compose(EpisodePair pair, AgentChooser chooser, in RewardSpec spec,
            Vector2 arenaCenter, BehaviorType behaviorType, ModelAsset model, Transform parent)
        {
            var host = new GameObject("[ShipAgent]");
            if (parent) host.transform.SetParent(parent, false);
            host.SetActive(false);

            var behavior = host.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = BehaviorName;
            behavior.BehaviorType = behaviorType;
            behavior.BrainParameters.VectorObservationSize = AgentObservations.Size;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(AgentActions.Count);
            behavior.DeterministicInference = true;
            if (model) behavior.Model = model;

            var agent = host.AddComponent<ShipAgent>();
            agent.Configure(pair.Agent, pair.Baseline, chooser, in spec, arenaCenter);
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
