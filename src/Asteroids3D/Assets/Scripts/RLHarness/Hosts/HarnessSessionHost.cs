using System;
using System.Collections;
using System.IO;
using Game.Capture;
using Game.Services;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for offline harness sessions, composed by TrainingBootstrap.RunHarnessSession in batch mode. It owns the measurement arena and two primitives — <see cref="NewComposition"/> (one per seed, so every RNG stream replays from that seed) and <see cref="RunBlock"/> (N consecutive episodes against one opponent config) — and the spec's lane client sequences them.</summary>
    public sealed class HarnessSessionHost : MonoBehaviour
    {
        [SerializeField] internal SessionSpec spec;
        [SerializeField] internal HarnessAssets assets;
        [SerializeField] internal ScriptableObject captureModule;
        [SerializeField] internal bool exitEditorWhenComplete;

        /// <summary>The measurement arena's in-plane offset; the lanes spawn their field here and the compositions build their world from it.</summary>
        internal Vector2 Offset => Vector2.zero;
        internal HarnessAssets Assets => assets;
        internal IProjectileService Projectiles { get; private set; }

        private UnitService units;
        private ISessionProbe[] probes;
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
                    "Game.Capture.GameView.GameViewEpisodeCapture, Game.Capture.GameView.Editor", throwOnError: true);
                var module = ScriptableObject.CreateInstance(type);
                module.hideFlags = HideFlags.HideAndDontSave;
                captureModule = module;
                episodeCapture = module as IEpisodeCapture;
            }
#endif
        }

        private IEnumerator Start()
        {
            // Before any ship spawns — embedded visual rigs self-gate on this at Awake. Presentation exists only when recording.
            Utils.GameSettings.SetPresentationEnabled(spec.Presentation);
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

        internal void Initialize(SessionSpec sessionSpec, HarnessAssets harnessAssets, UnitService unitService,
            IProjectileService projectiles, IEpisodeCapture capture = null)
        {
            spec = sessionSpec;
            assets = harnessAssets;
            units = unitService;
            Projectiles = projectiles;
            if (capture != null)
            {
                episodeCapture = capture;
                captureModule = capture as ScriptableObject;
            }
            probes = new ISessionProbe[sessionSpec.probes.Length];
            for (var i = 0; i < probes.Length; i++)
                probes[i] = SessionProbes.Create(sessionSpec.probes[i].name, sessionSpec.probes[i].ToParameters());
        }

        internal ISessionComposition NewComposition(in RewardSpec seedSpec, OpponentKind opponent, HarnessField field)
        {
            var world = World(field);
            return opponent switch
            {
                OpponentKind.Mirror => new PolicyPairComposition(units, world, Projectiles, assets, in seedSpec,
                    spec.model, spec.model, field),
                OpponentKind.Checkpoint => new PolicyPairComposition(units, world, Projectiles, assets, in seedSpec,
                    spec.model, spec.opponentModel, field),
                _ => new InferenceRosterComposition(units, world, Projectiles, assets, in seedSpec,
                    spec.model, field),
            };
        }

        internal ISessionComposition NewSentenceComposition(in RewardSpec seedSpec, HarnessField field,
            SentenceRow row) =>
            new SentenceComposition(units, World(field), Projectiles, assets, in seedSpec, field, row);

        private WorldHandle World(HarnessField field) => new(Offset, units.Registry, field?.Field);

        /// <summary>Episodes 0..N-1 against one opponent config — the index restarts per block, so blocks on one seed are a controlled comparison over the same poses and field layouts. When the spec records, each selected episode films through a per-episode recorder wired here.</summary>
        internal IEnumerator RunBlock(ISessionComposition composition, OpponentSpec opponent, int episodes,
            RewardSpec episodeSpec, string jsonlPath, Action<EpisodeResult> onEpisode)
        {
            ResolveEpisodeCapture();
            if (spec.record.enabled && episodeCapture == null)
                throw new InvalidOperationException(
                    "RL_HARNESS_RECORD selected capture, but the Editor bootstrap attached no episode-capture module.");
            for (var episode = 0; episode < episodes; episode++)
            {
                // Pinned install before RunEpisode's pair-reset (the respawn re-inits the brain).
                var draw = composition.InstallOpponent(in opponent, in episodeSpec, episode, Offset);
                var context = new ProbeContext(composition.Pair, Offset, in episodeSpec, episode, in draw,
                    opponent.Label, composition.Driver);
                var pair = composition.Pair;
                var captureConfig = spec.record.Records(episode)
                    ? ClipConfig(episodeSpec.runSeed, opponent.Label, episode, jsonlPath)
                    : null;
                Action onFixedStep = () =>
                {
                    SampleProbes();
                    if (captureConfig != null) episodeCapture.Step();
                };
                try
                {
                    yield return composition.Driver.RunEpisode(episodeSpec, episode,
                        onBegin: () =>
                        {
                            BeginProbes(context);
                            if (captureConfig != null)
                                episodeCapture.Begin(captureConfig, spec.gizmoProfile,
                                    new[] { pair.Agent, pair.Baseline }, Projectiles);
                        },
                        onFixedStep: onFixedStep);
                }
                finally
                {
                    if (captureConfig != null) episodeCapture.End();
                }
                // The trailing record is what puts the draw in the episode's JSONL row.
                composition.Driver.Runner.RecordOpponent(in draw);
                var result = composition.Driver.Runner.Result;
                EpisodeJsonl.Append(jsonlPath, in result);
                onEpisode?.Invoke(result);
                EndProbes(in result, jsonlPath);
            }
        }


        private CaptureConfig ClipConfig(int seed, string label, int episode, string jsonlPath) => new()
        {
            outputRoot = Path.GetDirectoryName(jsonlPath),
            runStamp = Path.GetFileNameWithoutExtension(jsonlPath),
            clipName = $"s{seed}-{label}-ep{episode:D2}",
            width = spec.record.width,
            height = spec.record.height,
            everyFixedSteps = spec.record.everyFixedSteps,
        };

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

        private static ISessionClient Client(SessionLane lane) => lane switch
        {
            SessionLane.Eval => new CheckpointEvaluator(),
            SessionLane.Capture => new CaptureClient(),
            SessionLane.Sentence => new SentenceLane(),
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
            Debug.LogError($"[HarnessSessionHost] fatal: {condition}\n{stackTrace}");
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
