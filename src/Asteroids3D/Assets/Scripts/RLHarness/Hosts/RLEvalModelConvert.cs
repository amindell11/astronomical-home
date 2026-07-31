#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>-executeMethod convert tollbooth for the player eval lane: import the candidate (and the checkpoint opponent, when RL_HARNESS_OPPONENT names one) into the eval fixture slots, then build the single-session model bundle into RL_HARNESS_OUT_DIR. Editor-only because ONNX→ModelAsset conversion is; the sim wall-clock moves to the player.</summary>
    public static class RLEvalModelConvert
    {
        public static void Convert()
        {
            var source = Environment.GetEnvironmentVariable("RL_HARNESS_ONNX")
                ?? throw new InvalidOperationException(
                    "RL_HARNESS_ONNX is unset — the convert step needs the candidate checkpoint file.");
            var outDir = Environment.GetEnvironmentVariable("RL_HARNESS_OUT_DIR")
                ?? throw new InvalidOperationException(
                    "RL_HARNESS_OUT_DIR is unset — the caller names the dir the bundle lands in.");
            var opponent = Environment.GetEnvironmentVariable("RL_HARNESS_OPPONENT");
            var checkpointOpponent = opponent != null && opponent.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase);

            // Explicit build list only — no persistent bundle tags enter the project.
            var build = new AssetBundleBuild
            {
                assetBundleName = EvalModelBundle.FileName,
                assetNames = checkpointOpponent
                    ? new[] { TrainingBootstrap.ImportEvalCandidate(source), TrainingBootstrap.ImportEvalOpponent(opponent) }
                    : new[] { TrainingBootstrap.ImportEvalCandidate(source) },
                addressableNames = checkpointOpponent
                    ? new[] { EvalModelBundle.CandidateAsset, EvalModelBundle.OpponentAsset }
                    : new[] { EvalModelBundle.CandidateAsset },
            };
            Directory.CreateDirectory(outDir);
            var manifest = BuildPipeline.BuildAssetBundles(outDir, new[] { build },
                BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);

            var ok = manifest != null;
            Debug.Log($"[RLEvalModelConvert] result={(ok ? "ok" : "FAILED")} "
                + $"bundle={Path.Combine(outDir, EvalModelBundle.FileName)} opponentIncluded={checkpointOpponent}");
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
            else if (!ok)
                throw new Exception("Model bundle build failed (see console).");
        }
    }
}
#endif
