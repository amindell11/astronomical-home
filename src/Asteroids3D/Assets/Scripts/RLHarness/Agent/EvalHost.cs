using System.Collections;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for checkpoint eval, composed by TrainingBootstrap.RunEval in batch mode; the default seed list is the pinned held-out set.</summary>
    public sealed class EvalHost : MonoBehaviour
    {
        [SerializeField] internal string onnxAssetPath = ShipAgentFactory.SmokeFixturePath;
        [SerializeField] internal int episodesPerSeed = 5;
        [SerializeField] internal string seedSelector = "held-out";

        private IEnumerator Start()
        {
            PacingContract.Apply();
            var seeds = EvalProtocol.ResolveSeeds(seedSelector, out var tag);
            var (units, arena) = HarnessArena.Compose(gameObject);
            yield return CheckpointEvaluator.Run(units, arena, onnxAssetPath,
                seeds, episodesPerSeed, RewardSpec.Default, tag, null);
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }
}
