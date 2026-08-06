using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>What a probe is handed at the start of each episode in a block.</summary>
    public readonly struct ProbeContext
    {
        public readonly EpisodePair pair;
        public readonly Vector2 arenaCenter;
        public readonly RewardSpec spec;
        public readonly int episodeIndex;
        public readonly OpponentDraw draw;
        public readonly string opponentLabel;
        public readonly IStepSnapshotSource snapshots;

        public ProbeContext(EpisodePair pair, Vector2 arenaCenter, in RewardSpec spec, int episodeIndex,
            in OpponentDraw draw, string opponentLabel, IStepSnapshotSource snapshots)
        {
            this.pair = pair;
            this.arenaCenter = arenaCenter;
            this.spec = spec;
            this.episodeIndex = episodeIndex;
            this.draw = draw;
            this.opponentLabel = opponentLabel;
            this.snapshots = snapshots;
        }
    }

    /// <summary>A session-scoped instrument selected by name (see <see cref="SessionProbes"/>): the host drives one Begin/Sample*/End cycle per episode and appends the returned row, then the lane client asks for the run's summary sidecar.</summary>
    public interface ISessionProbe : IDisposable
    {
        string Name { get; }
        void Begin(in ProbeContext context);
        void Sample();
        /// <summary>The episode's JSONL row; the host owns where it lands.</summary>
        string End(in EpisodeResult result);
        void Summarize(string summaryPath);
    }

    /// <summary>Where one probe's artifacts landed, carried by the run summary.</summary>
    [Serializable]
    public struct ProbeArtifacts
    {
        public string name;
        public string jsonl;
        public string summary;
    }

    /// <summary>The probe name registry — the selection grammar behind RL_HARNESS_PROBES. Each entry pairs its factory (taking the per-probe key→float param map) with the param keys it accepts, so the parse can refuse an unknown key before play mode.</summary>
    public static class SessionProbes
    {
        private static readonly Dictionary<string, (Func<IReadOnlyDictionary<string, float>, ISessionProbe> factory,
            string[] knownKeys)> Factories = new()
        {
            [ArchetypeGateProbe.ProbeName] = (_ => new ArchetypeGateProbe(), Array.Empty<string>()),
            [CombatTelemetryProbe.ProbeName] = (_ => new CombatTelemetryProbe(), Array.Empty<string>()),
            [ContactProbe.ProbeName] = (_ => new ContactProbe(), Array.Empty<string>()),
            [ControllerProbe.ProbeName] = (parameters => new ControllerProbe(parameters),
                new[] { ControllerProbe.YawRateDeadbandKey, ControllerProbe.TorqueDeadbandKey }),
            [FacingProbe.ProbeName] = (parameters => new FacingProbe(parameters),
                new[] { FacingProbe.AuthorityScaleKey }),
            [VelRebaseProbe.ProbeName] = (_ => new VelRebaseProbe(), Array.Empty<string>()),
        };

        public static string RegisteredNames => string.Join(", ", Factories.Keys);

        public static bool IsRegistered(string name) => name != null && Factories.ContainsKey(name);

        public static string[] KnownKeys(string name) =>
            Factories.TryGetValue(name, out var entry)
                ? entry.knownKeys
                : throw new ArgumentException($"No probe named '{name}'; registered probes: {RegisteredNames}.");

        public static ISessionProbe Create(string name, IReadOnlyDictionary<string, float> parameters = null) =>
            Factories.TryGetValue(name, out var entry)
                ? entry.factory(parameters)
                : throw new ArgumentException($"No probe named '{name}'; registered probes: {RegisteredNames}.");
    }
}
