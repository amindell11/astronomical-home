using System;
using System.Collections;
using System.IO;
using Game.Services;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Scene entry point for offline harness sessions, composed by TrainingBootstrap.RunEval in batch mode. It owns the measurement arena and two primitives — <see cref="NewComposition"/> (one per seed, so every RNG stream replays from that seed) and <see cref="RunBlock"/> (N consecutive episodes against one opponent config) — and the spec's lane client sequences them.</summary>
    public sealed class HarnessSessionHost : MonoBehaviour
    {
        [SerializeField] internal SessionSpec spec;
        [SerializeField] internal HarnessAssets assets;

        internal ArenaContext Arena { get; private set; }
        internal HarnessAssets Assets => assets;
        internal IProjectileService Projectiles { get; private set; }

        private UnitService units;
        private ISessionProbe[] probes;

        private IEnumerator Start()
        {
            // Before any ship spawns — embedded visual rigs self-gate on this at Awake.
            Utils.GameSettings.SetPresentationEnabled(false);
            PacingContract.Apply();
            // An exception inside a nested coroutine kills it silently; the batch would then hang until the caller's lease expires.
            Application.logMessageReceived += ExitOnException;

            var (composedUnits, arena, projectiles) = HarnessArena.Compose(gameObject, Vector2.zero,
                presentationEnabled: false);
            Initialize(spec, assets, composedUnits, arena, projectiles);
            yield return RunLane();
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
#endif
        }

        internal void Initialize(SessionSpec sessionSpec, HarnessAssets harnessAssets, UnitService unitService,
            ArenaContext arena, IProjectileService projectiles)
        {
            spec = sessionSpec;
            assets = harnessAssets;
            units = unitService;
            Arena = arena;
            Projectiles = projectiles;
            probes = new ISessionProbe[sessionSpec.probes.Length];
            for (var i = 0; i < probes.Length; i++) probes[i] = SessionProbes.Create(sessionSpec.probes[i]);
        }

        internal ISessionComposition NewComposition(in RewardSpec seedSpec, OpponentKind opponent, HarnessField field) =>
            opponent == OpponentKind.Mirror
                ? new MirrorComposition(units, Arena, Projectiles, assets, in seedSpec, spec.onnxAssetPath, field)
                : new InferenceRosterComposition(units, Arena, Projectiles, assets, in seedSpec, spec.onnxAssetPath,
                    field);

        /// <summary>Episodes 0..N-1 against one opponent config — the index restarts per block, so blocks on one seed are a controlled comparison over the same poses and field layouts.</summary>
        internal IEnumerator RunBlock(ISessionComposition composition, OpponentSpec opponent, int episodes,
            RewardSpec episodeSpec, string jsonlPath, Action<EpisodeResult> onEpisode)
        {
            for (var episode = 0; episode < episodes; episode++)
            {
                // Pinned install before RunEpisode's pair-reset (the respawn re-inits the chooser).
                var draw = composition.InstallOpponent(in opponent, in episodeSpec, episode, Arena.Offset);
                var context = new ProbeContext(composition.Pair, Arena.Offset, in episodeSpec, episode, in draw,
                    opponent.Label);
                yield return composition.Driver.RunEpisode(episodeSpec, episode,
                    onBegin: () => BeginProbes(context), onFixedStep: SampleProbes);
                // The trailing record is what puts the draw in the episode's JSONL row.
                composition.Driver.Runner.RecordOpponent(in draw);
                var result = composition.Driver.Runner.Result;
                EpisodeJsonl.Append(jsonlPath, in result);
                onEpisode?.Invoke(result);
                EndProbes(in result, jsonlPath);
            }
        }

        internal ProbeArtifacts[] SummarizeProbes(string jsonlPath)
        {
            var artifacts = new ProbeArtifacts[probes.Length];
            for (var i = 0; i < probes.Length; i++)
            {
                var summaryPath = ProbePath(jsonlPath, probes[i].Name, "-summary.json");
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

        private IEnumerator RunLane() => spec.lane switch
        {
            SessionLane.Eval => CheckpointEvaluator.RunLane(this, spec),
            _ => throw new NotSupportedException($"No lane client for {spec.lane}."),
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

        private static void ExitOnException(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception || !Application.isBatchMode) return;
#if UNITY_EDITOR
            Debug.LogError($"[HarnessSessionHost] fatal: {condition}\n{stackTrace}");
            UnityEditor.EditorApplication.Exit(1);
#endif
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= ExitOnException;
            if (probes == null) return;
            foreach (var probe in probes) probe.Dispose();
        }
    }
}
