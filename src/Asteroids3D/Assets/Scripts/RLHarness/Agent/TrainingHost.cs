using System;
using System.Collections;
using Ships.Command;
using Unity.MLAgents;
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
        [SerializeField] private HarnessAssets assets;
        [Tooltip("Self-play: opponent is a second team-1 ShipCombat agent (parameter-shared) instead of the scripted roster.")]
        [SerializeField] private bool selfPlay;

        private const uint WorkerSeedStream = 606;

        private IEpisodeComposition composition;

        private IEnumerator Start()
        {
            if (!assets)
                throw new InvalidOperationException("TrainingHost.assets is unset — assign the HarnessAssets catalog on the RLTraining scene's [TrainingHost].");

            PacingContract.Apply();
            StartCoroutine(PacingWatchdog());

            var workerIndex = ResolveWorkerIndex();
            var spec = RewardSpec.Default;
            spec.runSeed = DeriveWorkerSeed(runSeed, workerIndex ?? 0);
            if (Environment.GetEnvironmentVariable("RL_SMOKE") == "1")
                spec = SmokeSpec(spec);
            Func<string, float, float> envParams = Academy.Instance.EnvironmentParameters.GetWithDefault;
            spec = EnvParamOverlay.Apply(spec, envParams);

            composition = selfPlay || Environment.GetEnvironmentVariable("RL_SELFPLAY") == "1"
                ? new SelfPlayComposition(gameObject, in spec, behaviorType, assets)
                : (IEpisodeComposition)new ScriptedRosterComposition(gameObject, in spec, behaviorType, assets);
            var driver = composition.Driver;
            var jsonlPath = EpisodeJsonl.NewRunPath("training",
                dirOverride: CommandLineArg("--harness-jsonl-dir"),
                workerSuffix: workerIndex is int k ? $"-w{k}" : null);
            var terminals = 0;
            var truncations = 0;

            for (var i = 0; episodes <= 0 || i < episodes; i++)
            {
                // Re-read per episode: curriculum lessons move density/lethality/weights mid-run.
                var episodeSpec = EnvParamOverlay.Apply(spec, envParams);
                yield return driver.RunEpisode(episodeSpec, i);
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

        /// <summary>Per-worker run seed for --num-envs parallelism. Worker 0 (and editor/manual, k=0) is the identity, so it replays today's run byte-for-byte; k≥1 shifts the root seed, decorrelating poses/field/opponent downstream for free.</summary>
        public static int DeriveWorkerSeed(int baseSeed, int workerIndex) =>
            workerIndex == 0
                ? baseSeed
                : new SeedScope(baseSeed).Derive(WorkerSeedStream).Derive((uint)workerIndex).ToSeed();

        /// <summary>Worker index from the ML-Agents port offset. No --mlagents-port ⇒ null (editor/manual single env). Present ⇒ --harness-base-port is required and both must parse to a non-negative offset, else throw — a silent k=0 would re-correlate every worker's experience.</summary>
        private static int? ResolveWorkerIndex()
        {
            var portArg = CommandLineArg("--mlagents-port");
            if (portArg == null)
                return null;
            if (!int.TryParse(portArg, out var port))
                throw new InvalidOperationException($"--mlagents-port '{portArg}' is not an integer");
            var baseArg = CommandLineArg("--harness-base-port")
                ?? throw new InvalidOperationException("--mlagents-port is set but --harness-base-port is missing — a launched worker must receive the launcher's base port so its index can be derived (run_parallel.py passes it via --env-args)");
            if (!int.TryParse(baseArg, out var basePort))
                throw new InvalidOperationException($"--harness-base-port '{baseArg}' is not an integer");
            var k = port - basePort;
            if (k < 0)
                throw new InvalidOperationException($"worker index {k} is negative: --mlagents-port {port} < --harness-base-port {basePort}");
            return k;
        }

        private static string CommandLineArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private void OnDestroy() => composition?.Dispose();

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
