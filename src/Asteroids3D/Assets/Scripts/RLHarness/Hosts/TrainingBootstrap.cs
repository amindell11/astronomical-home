#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>-executeMethod entry points for attaching mlagents-learn to a batch-mode editor (launch without -quit; the run ends the process externally). The signaled variant exists because an editor boot outlasts the trainer's 60 s handshake window: boot and arm first, start the trainer, then create the flag file to enter play.</summary>
    public static class TrainingBootstrap
    {
        private const string EvalCandidateAssetPath = "Assets/Tests/Fixtures/EvalCandidate.onnx";
        private const string EvalOpponentAssetPath = "Assets/Tests/Fixtures/EvalOpponent.onnx";
        public static readonly string StartFlagPath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "..", "..", "results", "rl-training", "start-play.flag"));

        public static void EnterTrainingPlayMode()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/RLTraining.unity");
            EditorApplication.EnterPlaymode();
        }

        public static void EnterTrainingPlayModeWhenSignaled()
        {
            Debug.Log($"[TrainingBootstrap] armed; create {StartFlagPath} to enter play mode");
            EditorApplication.update += EnterOnFlag;
        }

        private static void EnterOnFlag()
        {
            if (!File.Exists(StartFlagPath)) return;
            EditorApplication.update -= EnterOnFlag;
            File.Delete(StartFlagPath);
            EnterTrainingPlayMode();
        }

        /// <summary>Harness-session batch entry (scripted eval at the gate shape): RL_HARNESS_ONNX names a checkpoint file to import (default: the committed smoke fixture), RL_HARNESS_EPISODES_PER_SEED the per-seed episode count, RL_HARNESS_SEEDS the seed selection ("held-out" default / "train" / comma list — see EvalProtocol.ResolveSeeds), RL_HARNESS_DENSITY a field-density override for stretch/diagnostic runs (default: the canonical eval env), RL_HARNESS_OPPONENT the opponent grammar ("roster" default / an archetype name / "mirror" / a checkpoint path ending .onnx, imported into the second fixture slot), RL_HARNESS_PROBES the comma-separated probe selection (default: "gate,combat"; "velrebase" on the open-loop lane), RL_HARNESS_OPENLOOP an archetype name or "all" to run the K1-2 velrebase lane instead of a checkpoint eval, RL_HARNESS_OUT_DIR the caller-owned absolute artifact dir (the lane launcher names it, then reads back the summary from it). The environment parses HERE so a malformed value fails before play mode; HarnessSessionHost exits the editor with code 0 when the summary artifact is written.</summary>
        public static void RunHarnessSession()
        {
            var spec = SessionSpec.ParseEval(Environment.GetEnvironmentVariable, ResolveEvalCandidate,
                ResolveEvalOpponent, () => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("[HarnessSessionHost]").AddComponent<HarnessSessionHost>();
            host.assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            host.spec = spec;
            EditorApplication.EnterPlaymode();
        }

        public static string ImportEvalCandidate(string sourceFile) => Import(sourceFile, EvalCandidateAssetPath);

        public static string ImportEvalOpponent(string sourceFile) => Import(sourceFile, EvalOpponentAssetPath);

        // A null source is the committed smoke fixture (the editor parse's test convenience).
        internal static Unity.InferenceEngine.ModelAsset ResolveEvalCandidate(string sourceFile) =>
            LoadModelAsset(sourceFile == null ? ShipAgentFactory.SmokeFixturePath : ImportEvalCandidate(sourceFile));

        internal static Unity.InferenceEngine.ModelAsset ResolveEvalOpponent(string sourceFile) =>
            LoadModelAsset(ImportEvalOpponent(sourceFile));

        private static Unity.InferenceEngine.ModelAsset LoadModelAsset(string assetPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(assetPath);
            if (!model)
                throw new InvalidOperationException($"Failed to load ONNX model at {assetPath}.");
            return model;
        }

        private static string Import(string sourceFile, string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            File.Copy(Path.GetFullPath(sourceFile), Path.Combine(projectRoot, assetPath), overwrite: true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return assetPath;
        }
    }
}
#endif
