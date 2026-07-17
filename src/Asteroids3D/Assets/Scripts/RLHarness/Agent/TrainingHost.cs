using System;
using System.Collections;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for RL training runs: composes the arena + episode pair + ShipAgent, applies and continuously asserts the pacing contract, and drives the episode loop from the WaitForFixedUpdate phase while mlagents-learn attaches to editor play mode.</summary>
    public sealed class TrainingHost : MonoBehaviour
    {
        [Tooltip("Episodes to run; 0 = until play mode stops.")]
        [SerializeField] private int episodes;
        [SerializeField] private int runSeed = EvalProtocol.TrainingRunSeed;
        [Tooltip("Default = trainer when connected, else heuristic. HeuristicOnly for a Python-free loop check. Checkpoint eval goes through CheckpointEvaluator, not this host.")]
        [SerializeField] private BehaviorType behaviorType = BehaviorType.Default;

        private IEnumerator Start()
        {
            PacingContract.Apply();
            StartCoroutine(PacingWatchdog());

            var spec = RewardSpec.Default;
            spec.runSeed = runSeed;
            if (Environment.GetEnvironmentVariable("RL_SMOKE") == "1")
                spec = SmokeSpec(spec);

            var (units, arena, projectiles) = HarnessArena.Compose(gameObject);
            var field = spec.useAsteroidField
                ? HarnessField.Spawn(arena, spec.fieldDensityScale, transform)
                : null;
            var pair = EpisodePair.SpawnWithAgentChooser(units, arena, projectiles, in spec, out var chooser);

            var agent = behaviorType switch
            {
                BehaviorType.Default => ShipAgentFactory.ComposeForTraining(pair, chooser, in spec, arena.Offset, transform),
                BehaviorType.HeuristicOnly => ShipAgentFactory.ComposeHeuristicOnly(pair, chooser, in spec, arena.Offset, transform),
                _ => throw new NotSupportedException(
                    $"TrainingHost supports Default (trainer) and HeuristicOnly; {behaviorType} checkpoint eval runs through CheckpointEvaluator."),
            };

            var driver = new EpisodeLoopDriver(pair, agent, arena.Offset, field);
            var jsonlPath = EpisodeJsonl.NewRunPath("training");
            var terminals = 0;
            var truncations = 0;

            for (var i = 0; episodes <= 0 || i < episodes; i++)
            {
                yield return driver.RunEpisode(spec, i);
                var result = driver.Runner.Result;
                EpisodeJsonl.Append(jsonlPath, in result);
                if (result.endKind == EndKind.Terminal.ToString()) terminals++;
                if (result.endKind == EndKind.Truncation.ToString()) truncations++;
                Debug.Log($"[TrainingHost] episode {i}: {result.outcome}/{result.endKind} "
                    + $"decisions={result.decisions} reward={result.totalReward:F3} "
                    + $"terminals={terminals} truncations={truncations}");
                if (i == 0)
                {
                    PacingContract.AssertHolds();
                    Debug.Log("[PacingContract] holds: one rendered frame advances exactly one fixed step under the resolved engine settings");
                }
            }
        }

        /// <summary>Trainer-smoke shape (RL_SMOKE=1): a tight arena and short clock so an untrained policy produces both terminal (out-of-bounds) and truncation (timeout) endings within the smoke's few thousand decisions.</summary>
        private static RewardSpec SmokeSpec(RewardSpec spec)
        {
            spec.arenaRadius = 25f;
            spec.timeoutDecisions = 30;
            spec.minSeparation = 18f;
            spec.maxSeparation = 24f;
            return spec;
        }

        private IEnumerator PacingWatchdog()
        {
            while (enabled)
            {
                PacingContract.AssertHolds();
                yield return null;
            }
        }
    }
}
