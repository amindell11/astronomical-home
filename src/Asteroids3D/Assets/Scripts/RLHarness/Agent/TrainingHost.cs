using System;
using System.Collections;
using System.Collections.Generic;
using Ships.Command;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for RL training runs: fans the arena composition out across M child arenas at spatial offsets, applies and continuously asserts the pacing contract, and drives one async episode loop per arena from the WaitForFixedUpdate phase while mlagents-learn attaches to editor play mode.</summary>
    public sealed class TrainingHost : MonoBehaviour
    {
        [Tooltip("Episodes to run per arena; 0 = until play mode stops.")]
        [SerializeField] private int episodes;
        [SerializeField] private int runSeed = EvalProtocol.TrainingRunSeed;
        [Tooltip("Default = trainer when connected, else heuristic. HeuristicOnly for a Python-free loop check. Checkpoint eval goes through CheckpointEvaluator, not this host.")]
        [SerializeField] private BehaviorType behaviorType = BehaviorType.Default;
        [SerializeField] private HarnessAssets assets;
        [Tooltip("Self-play: opponent is a second team-1 ShipCombat agent (parameter-shared) instead of the scripted roster.")]
        [SerializeField] private bool selfPlay;
        [Tooltip("In-process arenas fanned out at spatial offsets; --harness-num-arenas overrides.")]
        [SerializeField] private int numArenas = 1;

        private const uint WorkerSeedStream = 606;
        private const uint ArenaSeedStream = 707;

        // ≥ 2·arenaRadius(120) + laser maxDistance(50) + asteroid-drift margin, rounded up.
        public const float ArenaSpacing = 400f;

        private readonly List<IEpisodeComposition> compositions = new();
        private int terminals;
        private int truncations;

        private void Start()
        {
            if (!assets)
                throw new InvalidOperationException("TrainingHost.assets is unset — assign the HarnessAssets catalog on the RLTraining scene's [TrainingHost].");

            PacingContract.Apply();
            StartCoroutine(PacingWatchdog());

            var workerIndex = ResolveWorkerIndex();
            var arenaCount = ResolveNumArenas();
            var spec = RewardSpec.Default;
            spec.runSeed = DeriveWorkerSeed(runSeed, workerIndex ?? 0);
            if (Environment.GetEnvironmentVariable("RL_SMOKE") == "1")
                spec = SmokeSpec(spec);
            Func<string, float, float> envParams = Academy.Instance.EnvironmentParameters.GetWithDefault;
            spec = EnvParamOverlay.Apply(spec, envParams);
            var selfPlayRun = selfPlay || Environment.GetEnvironmentVariable("RL_SELFPLAY") == "1";

            for (var j = 0; j < arenaCount; j++)
            {
                var arenaHost = new GameObject($"Arena{j}");
                arenaHost.transform.SetParent(transform, false);
                var arenaSpec = spec;
                arenaSpec.runSeed = DeriveArenaSeed(spec.runSeed, j);
                var composition = selfPlayRun
                    ? new SelfPlayComposition(arenaHost, in arenaSpec, behaviorType, assets, ArenaOffset(j))
                    : (IEpisodeComposition)new ScriptedRosterComposition(arenaHost, in arenaSpec, behaviorType, assets, ArenaOffset(j));
                compositions.Add(composition);
                var jsonlPath = EpisodeJsonl.NewRunPath("training",
                    dirOverride: CommandLineArg("--harness-jsonl-dir"),
                    workerSuffix: ComposeSuffix(workerIndex, j, arenaCount));
                StartCoroutine(ArenaLoop(composition.Driver, arenaSpec, envParams, jsonlPath, j));
            }
        }

        private IEnumerator ArenaLoop(EpisodeLoopDriver driver, RewardSpec spec,
            Func<string, float, float> envParams, string jsonlPath, int arenaIndex)
        {
            for (var i = 0; episodes <= 0 || i < episodes; i++)
            {
                // Re-read per episode: curriculum lessons move density/lethality/weights mid-run.
                var episodeSpec = EnvParamOverlay.Apply(spec, envParams);
                yield return driver.RunEpisode(episodeSpec, i);
                var result = driver.Runner.Result;
                EpisodeJsonl.Append(jsonlPath, in result);
                if (result.endKind == EndKind.Terminal.ToString()) terminals++;
                if (result.endKind == EndKind.Truncation.ToString()) truncations++;
                Debug.Log($"[TrainingHost] arena {arenaIndex} episode {i}: {result.outcome}/{result.endKind} "
                    + $"decisions={result.decisions} reward={result.totalReward:F3} "
                    + $"terminals={terminals} truncations={truncations}");
                if (arenaIndex == 0 && i == 0)
                {
                    PacingContract.AssertHolds();
                    Debug.Log("[PacingContract] holds: one rendered frame advances exactly one fixed step under the resolved engine settings");
                }
            }
        }

        public static Vector2 ArenaOffset(int arenaIndex) => new(arenaIndex * ArenaSpacing, 0f);

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

        /// <summary>Per-arena run seed layered on the worker seed (base → worker → arena). Arena 0 is the identity, so M=1 replays today's run byte-for-byte; j≥1 shifts the root seed, decorrelating poses/field/opponent downstream for free.</summary>
        public static int DeriveArenaSeed(int workerSeed, int arenaIndex) =>
            arenaIndex == 0
                ? workerSeed
                : new SeedScope(workerSeed).Derive(ArenaSeedStream).Derive((uint)arenaIndex).ToSeed();

        /// <summary>Composed JSONL suffix: worker part when launched under a trainer port, arena part only when fanning out — M=1 filenames stay byte-identical to today's.</summary>
        private static string ComposeSuffix(int? workerIndex, int arenaIndex, int arenaCount)
        {
            var suffix = (workerIndex is int k ? $"-w{k}" : "") + (arenaCount > 1 ? $"-a{arenaIndex}" : "");
            return suffix.Length == 0 ? null : suffix;
        }

        /// <summary>Arena count from --harness-num-arenas, else the serialized value; either present-but-invalid or a value below 1 throws — a silent fallback to 1 would quietly discard the requested fan-out.</summary>
        private int ResolveNumArenas()
        {
            var arg = CommandLineArg("--harness-num-arenas");
            if (arg == null)
            {
                if (numArenas < 1)
                    throw new InvalidOperationException($"numArenas {numArenas} on the RLTraining scene's [TrainingHost] must be >= 1");
                return numArenas;
            }
            if (!int.TryParse(arg, out var m) || m < 1)
                throw new InvalidOperationException($"--harness-num-arenas '{arg}' must be an integer >= 1");
            return m;
        }

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

        private void OnDestroy()
        {
            foreach (var composition in compositions)
                composition.Dispose();
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
