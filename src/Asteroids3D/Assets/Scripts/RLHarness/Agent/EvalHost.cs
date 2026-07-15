using System.Collections;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for held-out checkpoint eval (composed by TrainingBootstrap.RunHeldOutEval in batch mode): pacing contract on, CheckpointEvaluator over the pinned held-out seeds, editor exit 0 on completion.</summary>
    public sealed class EvalHost : MonoBehaviour
    {
        [SerializeField] internal string onnxAssetPath = ShipAgentFactory.SmokeFixturePath;
        [SerializeField] internal int episodesPerSeed = 5;

        private IEnumerator Start()
        {
            PacingContract.Apply();
            var (units, arena) = HarnessArena.Compose(gameObject);
            yield return CheckpointEvaluator.Run(units, arena, onnxAssetPath,
                EvalProtocol.HeldOutSeeds, episodesPerSeed, RewardSpec.Default, "held-out", null);
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }
}
