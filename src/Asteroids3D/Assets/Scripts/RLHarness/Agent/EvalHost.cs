using System.Collections;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for checkpoint eval (composed by TrainingBootstrap.RunEval in batch mode): pacing contract on, CheckpointEvaluator over the configured seed list (default: the pinned held-out set), editor exit 0 on completion.</summary>
    public sealed class EvalHost : MonoBehaviour
    {
        [SerializeField] internal string onnxAssetPath = ShipAgentFactory.SmokeFixturePath;
        [SerializeField] internal int episodesPerSeed = 5;
        [SerializeField] internal int[] seeds = EvalProtocol.HeldOutSeeds;
        [SerializeField] internal string seedsTag = "held-out";

        private IEnumerator Start()
        {
            PacingContract.Apply();
            var (units, arena) = HarnessArena.Compose(gameObject);
            yield return CheckpointEvaluator.Run(units, arena, onnxAssetPath,
                seeds, episodesPerSeed, RewardSpec.Default, seedsTag, null);
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }
}
