#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.RLHarness
{
    // ONNX conversion is editor-only; player simulation is not.
    public static class RLEvalModelConvert
    {
        public static void Convert()
        {
            var source = Environment.GetEnvironmentVariable("RL_HARNESS_ONNX")
                ?? throw new InvalidOperationException(
                    "RL_HARNESS_ONNX is unset — the convert step needs the candidate checkpoint file.");
            var bundlePath = Environment.GetEnvironmentVariable("RL_HARNESS_BUNDLE")
                ?? throw new InvalidOperationException(
                    "RL_HARNESS_BUNDLE is unset — the caller names the bundle output path.");
            bundlePath = Path.GetFullPath(bundlePath);
            var outDir = Path.GetDirectoryName(bundlePath);
            var bundleName = Path.GetFileName(bundlePath);
            if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(bundleName))
                throw new ArgumentException($"RL_HARNESS_BUNDLE does not name a bundle file: {bundlePath}");
            var opponent = Environment.GetEnvironmentVariable("RL_HARNESS_OPPONENT");
            var checkpointOpponent = opponent != null && opponent.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase);

            // Explicit build list only — no persistent bundle tags enter the project.
            var build = new AssetBundleBuild
            {
                assetBundleName = bundleName,
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
                + $"bundle={bundlePath} opponentIncluded={checkpointOpponent}");
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
            else if (!ok)
                throw new Exception("Model bundle build failed (see console).");
        }
    }
}
#endif
