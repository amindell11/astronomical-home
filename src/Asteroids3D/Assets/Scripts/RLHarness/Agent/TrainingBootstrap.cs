#if UNITY_EDITOR
using System;
using System.Globalization;
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

        /// <summary>Checkpoint-eval batch entry: RL_EVAL_ONNX names a checkpoint file to import (default: the committed smoke fixture), RL_EVAL_EPISODES_PER_SEED the per-seed episode count, RL_EVAL_SEEDS the seed selection ("held-out" default / "train" / comma list — see EvalProtocol.ResolveSeeds), RL_EVAL_DENSITY a field-density override for stretch/diagnostic runs (default: the canonical eval env), RL_EVAL_OUT_DIR the caller-owned absolute artifact dir (the eval gate names it, then reads back the summary from it). EvalHost exits the editor with code 0 when the summary artifact is written.</summary>
        public static void RunEval()
        {
            var source = Environment.GetEnvironmentVariable("RL_EVAL_ONNX");
            var assetPath = string.IsNullOrEmpty(source)
                ? ShipAgentFactory.SmokeFixturePath
                : ImportEvalCandidate(source);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("[EvalHost]").AddComponent<EvalHost>();
            host.assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            host.onnxAssetPath = assetPath;
            if (int.TryParse(Environment.GetEnvironmentVariable("RL_EVAL_EPISODES_PER_SEED"), out var episodes))
                host.episodesPerSeed = episodes;
            var selector = Environment.GetEnvironmentVariable("RL_EVAL_SEEDS");
            if (!string.IsNullOrEmpty(selector)) host.seedSelector = selector;
            var density = Environment.GetEnvironmentVariable("RL_EVAL_DENSITY");
            if (!string.IsNullOrEmpty(density))
                host.fieldDensityScale = float.Parse(density, CultureInfo.InvariantCulture);
            var outDir = Environment.GetEnvironmentVariable("RL_EVAL_OUT_DIR");
            if (!string.IsNullOrEmpty(outDir)) host.outDirOverride = outDir;
            EditorApplication.EnterPlaymode();
        }

        public static string ImportEvalCandidate(string sourceFile)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            File.Copy(Path.GetFullPath(sourceFile), Path.Combine(projectRoot, EvalCandidateAssetPath), overwrite: true);
            AssetDatabase.ImportAsset(EvalCandidateAssetPath, ImportAssetOptions.ForceUpdate);
            return EvalCandidateAssetPath;
        }
    }
}
#endif
