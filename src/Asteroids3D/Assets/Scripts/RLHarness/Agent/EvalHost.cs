using System.Collections;
using System.Globalization;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for checkpoint eval, composed by TrainingBootstrap.RunEval in batch mode; the default seed list is the pinned held-out set and the default environment is the canonical eval env (training's terminal lesson).</summary>
    public sealed class EvalHost : MonoBehaviour
    {
        [SerializeField] internal string onnxAssetPath = ShipAgentFactory.SmokeFixturePath;
        [SerializeField] internal int episodesPerSeed = 5;
        [SerializeField] internal string seedSelector = "held-out";
        [SerializeField] internal float fieldDensityScale = EvalProtocol.CanonicalFieldDensityScale;
        [SerializeField] internal string outDirOverride;
        [SerializeField] internal HarnessAssets assets;

        private IEnumerator Start()
        {
            // Before any ship spawns — embedded visual rigs self-gate on this at Awake.
            Utils.GameSettings.SetPresentationEnabled(false);

            PacingContract.Apply();
            var seeds = EvalProtocol.ResolveSeeds(seedSelector, out var tag);
            // A non-canonical density (the 3.0 stretch) marks its artifacts so it can never pass as the canonical eval.
            if (!Mathf.Approximately(fieldDensityScale, EvalProtocol.CanonicalFieldDensityScale))
                tag += "-d" + fieldDensityScale.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_');
            var (units, arena, projectiles) = HarnessArena.Compose(gameObject, Vector2.zero, presentationEnabled: false);
            yield return CheckpointEvaluator.Run(units, arena, projectiles, assets, onnxAssetPath,
                seeds, episodesPerSeed, EvalProtocol.EvalSpec(fieldDensityScale), tag, null, outDirOverride);
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }
}
