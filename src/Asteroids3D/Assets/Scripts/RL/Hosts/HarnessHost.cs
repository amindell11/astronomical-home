using System;
using System.Collections;
using System.IO;
using Capture;
using Capture.Recording;
using Capture.Views;
using Combat.Weapons;
using Substrate.Services;
using UnityEngine;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Arena;
using RL.Episodes;
using RL.Episodes.Compositions;
using RL.Hosts.Lanes;
using RL.Opponents;
using RL.Probes;
using RL.Reward;

namespace RL.Hosts
{
    /// <summary>Scene entry point for one offline harness run, composed by TrainingBootstrap.RunHarness in batch mode. It owns the measurement arena and two primitives — <see cref="NewComposition"/> (one per seed, so every RNG stream replays from that seed) and <see cref="RunBlock"/> (N consecutive episodes against one opponent config) — and the spec's lane client sequences them.</summary>
    public sealed class HarnessHost : MonoBehaviour
    {
        [SerializeField] internal HarnessSpec spec;
        [SerializeField] internal HarnessAssets assets;
        [SerializeField] internal ScriptableObject captureModule;
        [SerializeField] internal bool exitEditorWhenComplete;

        /// <summary>The measurement arena sits at the plane origin; lanes and compositions place their world from this.</summary>
        internal Vector2 Offset => Vector2.zero;
        internal HarnessAssets Assets => assets;
        internal IProjectileService Projectiles { get; private set; }

        private UnitService units;
        private IHarnessProbe[] probes;
        private IEpisodeCapture episodeCapture;
        internal bool HasEpisodeCapture => episodeCapture != null;

        // Resolved lazily: the bootstrap assigns captureModule AFTER AddComponent has already run
        // Awake, and the play-mode domain reload destroys the edit-mode HideAndDontSave instance
        // anyway — so a recording session recreates the module on the play-mode side.
        private void Awake() => ResolveEpisodeCapture();

        private void ResolveEpisodeCapture()
        {
            episodeCapture ??= captureModule as IEpisodeCapture;
#if UNITY_EDITOR
            if (episodeCapture == null && spec != null && spec.record.enabled)
            {
                var type = System.Type.GetType(
                    "Capture.GameView.GameViewEpisodeCapture, Capture.GameView.Editor", throwOnError: true);
                var module = ScriptableObject.CreateInstance(type);
                module.hideFlags = HideFlags.HideAndDontSave;
                captureModule = module;
                episodeCapture = module as IEpisodeCapture;
            }
#endif
        }

        private IEnumerator Start()
        {
            PacingContract.Apply();
            // An exception inside a nested coroutine kills it silently; the batch would then hang until the caller's lease expires.
            Application.logMessageReceived += ExitOnException;

            var composedUnits = gameObject.AddComponent<UnitService>();
            var projectiles = ShipServices.Compose(composedUnits, transform, spec.Presentation);
            Initialize(spec, assets, composedUnits, projectiles);
            yield return RunLane();
#if UNITY_EDITOR
            if (exitEditorWhenComplete) UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        internal void Initialize(HarnessSpec harnessSpec, HarnessAssets harnessAssets, UnitService unitService,
            IProjectileService projectiles, IEpisodeCapture capture = null)
        {
            spec = harnessSpec;
            assets = harnessAssets;
            units = unitService;
            Projectiles = projectiles;
            if (capture != null)
            {
                episodeCapture = capture;
                captureModule = capture as ScriptableObject;
            }
            probes = new IHarnessProbe[harnessSpec.probes.Length];
            for (var i = 0; i < probes.Length; i++)
                probes[i] = HarnessProbes.Create(harnessSpec.probes[i].name, harnessSpec.probes[i].ToParameters());
        }

        internal IHarnessComposition NewComposition(in RewardSpec seedSpec, OpponentKind opponent, HarnessField field)
        {
            return opponent switch
            {
                OpponentKind.Mirror => new PolicyPairComposition(units, Offset, Projectiles, assets, in seedSpec,
                    spec.model, spec.model, field),
                OpponentKind.Checkpoint => new PolicyPairComposition(units, Offset, Projectiles, assets, in seedSpec,
                    spec.model, spec.opponentModel, field),
                _ => new InferenceRosterComposition(units, Offset, Projectiles, assets, in seedSpec,
                    spec.model, field),
            };
        }

        internal IHarnessComposition NewSentenceComposition(in RewardSpec seedSpec, HarnessField field,
            SentenceRow row) =>
            new SentenceComposition(units, Offset, Projectiles, assets, in seedSpec, field, row);

        internal IHarnessComposition NewDuelComposition(in RewardSpec seedSpec, WeaponComponent weapon) =>
            new DuelComposition(units, Offset, Projectiles, assets, in seedSpec, weapon);

        internal IHarnessComposition NewMatrixComposition(in RewardSpec seedSpec, WeaponComponent weaponA,
            WeaponComponent weaponB) =>
            new MatrixComposition(units, Offset, Projectiles, assets, in seedSpec, weaponA, weaponB);

        /// <summary>Episodes 0..N-1 against one opponent config — the index restarts per block, so blocks on one seed are a controlled comparison over the same poses and field layouts. When the spec records, each selected episode films through a per-episode recorder wired here.</summary>
        internal IEnumerator RunBlock(IHarnessComposition composition, OpponentSpec opponent, int episodes,
            RewardSpec episodeSpec, string jsonlPath, Action<EpisodeResult> onEpisode)
        {
            ResolveEpisodeCapture();
            if (spec.record.enabled && episodeCapture == null)
                throw new InvalidOperationException(
                    "RL_HARNESS_RECORD selected capture, but the Editor bootstrap attached no episode-capture module.");
            for (var episode = 0; episode < episodes; episode++)
            {
                var pair = composition.Pair;
                using var pacing = spec.record.Records(episode) ? CapturePacing.Locked(spec.record.everyFixedSteps) : null;
                using var view = spec.record.Records(episode)
                    ? ((IDiagnosticViewFactory)episodeCapture).PrepareView(new DiagnosticViewConfig(), spec.gizmoProfile,
                        new[] { pair.Agent, pair.Baseline }, Projectiles)
                    : null;
                using var recording = spec.record.Records(episode)
                    ? episodeCapture.Prepare(ClipRequest(episodeSpec.runSeed, opponent.Label, episode, jsonlPath))
                    : null;
                if (recording != null) yield return recording.Ready;
                // Pinned install before RunEpisode's pair-reset (the respawn re-inits the brain).
                var draw = composition.InstallOpponent(in opponent, in episodeSpec, episode);
                var context = new ProbeContext(composition.Pair, Offset, in episodeSpec, episode, in draw,
                    opponent.Label, composition.Driver);
                yield return composition.Driver.RunEpisode(episodeSpec, episode,
                    onBegin: () =>
                    {
                        BeginProbes(context);
                        recording?.Start();
                    },
                    onFixedStep: SampleProbes);
                recording?.Finish();
                // The trailing record is what puts the draw in the episode's JSONL row.
                composition.Driver.Runner.RecordOpponent(in draw);
                var result = composition.Driver.Runner.Result;
                EpisodeJsonl.Append(jsonlPath, in result);
                onEpisode?.Invoke(result);
                EndProbes(in result, jsonlPath);
            }
        }


        private CaptureRequest ClipRequest(int seed, string label, int episode, string jsonlPath) => new(
            Path.GetDirectoryName(jsonlPath), $"s{seed}-{label}-ep{episode:D2}", spec.captureCommit,
            spec.record.width, spec.record.height, Path.GetFileNameWithoutExtension(jsonlPath));

        internal ProbeArtifacts[] SummarizeProbes(string jsonlPath)
        {
            var artifacts = new ProbeArtifacts[probes.Length];
            for (var i = 0; i < probes.Length; i++)
            {
                // Not "-summary.json": eval_gate globs *-summary.json expecting exactly the one eval summary.
                var summaryPath = ProbePath(jsonlPath, probes[i].Name, "-probe.json");
                probes[i].Summarize(summaryPath);
                artifacts[i] = new ProbeArtifacts
                {
                    name = probes[i].Name,
                    jsonl = ProbePath(jsonlPath, probes[i].Name, ".jsonl"),
                    summary = summaryPath,
                };
            }
            return artifacts;
        }

        private IEnumerator RunLane() => Client(spec.lane).Run(this, spec);

        private static ILaneClient Client(HarnessLane lane) => lane switch
        {
            HarnessLane.Eval => new CheckpointEvaluator(),
            HarnessLane.Capture => new CaptureClient(),
            HarnessLane.Sentence => new SentenceLane(),
            HarnessLane.Duel => new DuelLane(),
            HarnessLane.Matrix => new MatrixLane(),
            _ => throw new NotSupportedException($"No lane client for {lane}."),
        };

        private void BeginProbes(ProbeContext context)
        {
            foreach (var probe in probes) probe.Begin(in context);
        }

        private void SampleProbes()
        {
            foreach (var probe in probes) probe.Sample();
        }

        private void EndProbes(in EpisodeResult result, string jsonlPath)
        {
            foreach (var probe in probes)
                File.AppendAllText(ProbePath(jsonlPath, probe.Name, ".jsonl"), probe.End(in result) + "\n");
        }

        private static string ProbePath(string jsonlPath, string probeName, string suffix) =>
            jsonlPath.Replace(".jsonl", $"-{probeName}{suffix}");

        private void ExitOnException(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception || !exitEditorWhenComplete) return;
            Debug.LogError($"[HarnessHost] fatal: {condition}\n{stackTrace}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(1);
#else
            Application.Quit(1);
#endif
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= ExitOnException;
            if (probes != null)
                foreach (var probe in probes) probe.Dispose();
            if (captureModule) Destroy(captureModule);
        }
    }
}
