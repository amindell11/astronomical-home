using System;
using System.Collections;
using System.IO;
using Game.Capture;
using Game.Diagnostics;
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

        internal ArenaContext Arena { get; private set; }
        internal HarnessAssets Assets => assets;
        internal IProjectileService Projectiles { get; private set; }

        private UnitService units;
        private ISessionProbe[] probes;
        private IEpisodeCapture episodeCapture;
        internal bool HasEpisodeCapture => episodeCapture != null;

        private void Awake() => episodeCapture = captureModule as IEpisodeCapture;

        private IEnumerator Start()
        {
            // Before any ship spawns — embedded visual rigs self-gate on this at Awake. Presentation exists only when recording.
            Utils.GameSettings.SetPresentationEnabled(spec.Presentation);
            PacingContract.Apply();
            // An exception inside a nested coroutine kills it silently; the batch would then hang until the caller's lease expires.
            Application.logMessageReceived += ExitOnException;

            var (composedUnits, arena, projectiles) = HarnessArena.Compose(gameObject, Vector2.zero,
                presentationEnabled: spec.Presentation);
            Initialize(spec, assets, composedUnits, arena, projectiles);
            yield return RunLane();
#if UNITY_EDITOR
            if (exitEditorWhenComplete) UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        internal void Initialize(SessionSpec sessionSpec, HarnessAssets harnessAssets, UnitService unitService,
            ArenaContext arena, IProjectileService projectiles, IEpisodeCapture capture = null)
        {
            spec = sessionSpec;
            assets = harnessAssets;
            units = unitService;
            Arena = arena;
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

        internal ISessionComposition NewComposition(in RewardSpec seedSpec, OpponentKind opponent, HarnessField field) =>
            opponent switch
            {
                OpponentKind.Mirror => new PolicyPairComposition(units, Arena, Projectiles, assets, in seedSpec,
                    spec.model, spec.model, field),
                OpponentKind.Checkpoint => new PolicyPairComposition(units, Arena, Projectiles, assets, in seedSpec,
                    spec.model, spec.opponentModel, field),
                _ => new InferenceRosterComposition(units, Arena, Projectiles, assets, in seedSpec,
                    spec.model, field),
            };

        internal ISessionComposition NewOpenLoopComposition(in RewardSpec seedSpec, HarnessField field) =>
            new OpenLoopArchetypeComposition(units, Arena, Projectiles, assets, in seedSpec, field);

        /// <summary>Episodes 0..N-1 against one opponent config — the index restarts per block, so blocks on one seed are a controlled comparison over the same poses and field layouts. When the spec records, each selected episode films through a per-episode recorder wired here.</summary>
        internal IEnumerator RunBlock(ISessionComposition composition, OpponentSpec opponent, int episodes,
            RewardSpec episodeSpec, string jsonlPath, Action<EpisodeResult> onEpisode)
        {
            var nativeCapture = spec.gizmoProfile != GizmoCaptureProfile.None;
            if (nativeCapture && episodeCapture == null)
                throw new InvalidOperationException(
                    "RL_HARNESS_GIZMOS selected native capture, but the Editor bootstrap attached no episode-capture module.");
            var drawPainters = nativeCapture ? null : BuildPainterDraw(composition);
            var subjects = new Vector2[2];
            for (var episode = 0; episode < episodes; episode++)
            {
                // Pinned install before RunEpisode's pair-reset (the respawn re-inits the brain).
                var draw = composition.InstallOpponent(in opponent, in episodeSpec, episode, Arena.Offset);
                var context = new ProbeContext(composition.Pair, Arena.Offset, in episodeSpec, episode, in draw,
                    opponent.Label, composition.Driver);
                var records = spec.record.Records(episode);
                using var recorder = records && !nativeCapture
                    ? new CaptureRecorder(ClipConfig(episodeSpec.runSeed, opponent.Label, episode, jsonlPath))
                    : null;
                var pair = composition.Pair;
                var captureConfig = records && nativeCapture
                    ? ClipConfig(episodeSpec.runSeed, opponent.Label, episode, jsonlPath)
                    : null;
                Action onFixedStep = () =>
                {
                    SampleProbes();
                    if (records && nativeCapture)
                    {
                        episodeCapture.Step();
                    }
                    else if (recorder != null)
                    {
                        subjects[0] = pair.Agent.Kinematics.pos;
                        subjects[1] = pair.Baseline.Kinematics.pos;
                        recorder.Step(subjects, drawPainters);
                    }
                };
                try
                {
                    yield return composition.Driver.RunEpisode(episodeSpec, episode,
                        onBegin: () =>
                        {
                            BeginProbes(context);
                            if (captureConfig != null)
                                episodeCapture.Begin(captureConfig, spec.gizmoProfile, pair.Agent, pair.Baseline,
                                    Projectiles);
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

        // Painters bind the pair's ships at construction; a null draw when recording is off costs nothing.
        private Action<CaptureDraw> BuildPainterDraw(ISessionComposition composition)
        {
            if (!spec.record.enabled) return null;
            var context = new PainterContext(composition.Pair.Agent, composition.Pair.Baseline, Projectiles);
            var painters = new IDiagnosticPainter[spec.painters.Length];
            for (var i = 0; i < painters.Length; i++)
                painters[i] = DiagnosticPainters.Create(spec.painters[i], in context);
            return canvas =>
            {
                foreach (var painter in painters) painter.Paint(canvas);
            };
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
            SessionLane.OpenLoop => new VelRebaseLane(),
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
