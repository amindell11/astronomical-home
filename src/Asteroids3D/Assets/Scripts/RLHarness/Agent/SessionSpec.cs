using System;
using System.Globalization;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Which lane client the host runs; every other axis of a session is a spec field.</summary>
    public enum SessionLane { Eval }

    public enum OpponentKind { Roster, Archetype, Mirror }

    /// <summary>One block's opponent: a pinned scripted archetype, or the checkpoint's own mirror.</summary>
    public struct OpponentSpec
    {
        public const string MirrorLabel = "Mirror";

        public OpponentKind kind;
        public OpponentArchetype archetype;

        public static readonly OpponentSpec Mirror = new() { kind = OpponentKind.Mirror };

        public static OpponentSpec Pinned(OpponentArchetype archetype) =>
            new() { kind = OpponentKind.Archetype, archetype = archetype };

        public string Label => kind == OpponentKind.Mirror ? MirrorLabel : archetype.ToString();
    }

    /// <summary>A harness session's fully-resolved configuration, parsed from the environment ONCE at the batch boundary — before play mode — so a malformed value fails there instead of inside a running episode loop. Carried into play mode as a serialized field on <see cref="HarnessSessionHost"/>.</summary>
    [Serializable]
    public sealed class SessionSpec
    {
        public const string RosterToken = "roster";
        public const string MirrorToken = "mirror";
        public const int DefaultEpisodesPerSeed = 5;

        public SessionLane lane;
        public string onnxAssetPath;
        public string onnxSourcePath;
        public int[] seeds;
        public string tag;
        public int episodesPerSeed;
        public float fieldDensityScale;
        public OpponentKind opponentKind;
        public OpponentArchetype opponentArchetype;
        public string[] probes;
        public string outDir;

        /// <summary>Parses the eval lane's environment. <paramref name="importCheckpoint"/> imports RL_EVAL_ONNX into the fixture slot and returns its asset path (AssetDatabase work the parse itself stays free of).</summary>
        public static SessionSpec ParseEval(Func<string, string> getEnv, Func<string, string> importCheckpoint)
        {
            var source = getEnv("RL_EVAL_ONNX");
            var spec = new SessionSpec
            {
                lane = SessionLane.Eval,
                onnxSourcePath = source,
                onnxAssetPath = string.IsNullOrEmpty(source)
                    ? ShipAgentFactory.SmokeFixturePath
                    : importCheckpoint(source),
                episodesPerSeed = ParseEpisodes(getEnv("RL_EVAL_EPISODES_PER_SEED")),
                fieldDensityScale = ParseDensity(getEnv("RL_EVAL_DENSITY")),
                probes = ParseProbes(getEnv("RL_EVAL_PROBES")),
                outDir = getEnv("RL_EVAL_OUT_DIR"),
            };
            spec.seeds = ParseSeeds(getEnv("RL_EVAL_SEEDS"), out var tag);
            // A non-canonical density (the 3.0 stretch) marks its artifacts so it can never pass as the canonical eval.
            spec.tag = Mathf.Approximately(spec.fieldDensityScale, EvalProtocol.CanonicalFieldDensityScale)
                ? tag
                : tag + "-d" + spec.fieldDensityScale.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_');
            spec.ParseOpponent(getEnv("RL_EVAL_OPPONENT"));
            return spec;
        }

        private static int ParseEpisodes(string value)
        {
            if (string.IsNullOrEmpty(value)) return DefaultEpisodesPerSeed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var episodes)
                || episodes < 1)
                throw new ArgumentException($"RL_EVAL_EPISODES_PER_SEED='{value}' is not a positive episode count.");
            return episodes;
        }

        private static float ParseDensity(string value)
        {
            if (string.IsNullOrEmpty(value)) return EvalProtocol.CanonicalFieldDensityScale;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var density))
                throw new ArgumentException($"RL_EVAL_DENSITY='{value}' is not a number.");
            return density;
        }

        private static int[] ParseSeeds(string selector, out string tag)
        {
            try
            {
                return EvalProtocol.ResolveSeeds(selector, out tag);
            }
            catch (FormatException inner)
            {
                throw new ArgumentException(
                    $"RL_EVAL_SEEDS='{selector}' is not \"held-out\", \"train\", or a comma-separated seed list.", inner);
            }
        }

        private static string[] ParseProbes(string value)
        {
            if (value == null) return new[] { ArchetypeGateProbe.ProbeName };
            var names = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = names[i].Trim();
                if (!SessionProbes.IsRegistered(names[i]))
                    throw new ArgumentException(
                        $"RL_EVAL_PROBES names '{names[i]}'; registered probes: {SessionProbes.RegisteredNames}.");
            }
            return names;
        }

        private void ParseOpponent(string token)
        {
            if (string.IsNullOrEmpty(token) || Matches(token, RosterToken)) return;
            if (Matches(token, MirrorToken))
            {
                opponentKind = OpponentKind.Mirror;
                return;
            }
            if (token.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"RL_EVAL_OPPONENT='{token}': the second checkpoint slot lands in slice C.");
            if (!Enum.TryParse<OpponentArchetype>(token, ignoreCase: true, out var archetype))
                throw new ArgumentException($"RL_EVAL_OPPONENT='{token}' is not \"{RosterToken}\", \"{MirrorToken}\", "
                    + $"or one of {string.Join(", ", Enum.GetNames(typeof(OpponentArchetype)))}.");
            opponentKind = OpponentKind.Archetype;
            opponentArchetype = archetype;
        }

        private static bool Matches(string token, string keyword) =>
            string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);
    }
}
