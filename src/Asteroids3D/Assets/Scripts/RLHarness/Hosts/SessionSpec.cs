using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Diagnostics;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Which lane client the host runs; every other axis of a session is a spec field.</summary>
    public enum SessionLane { Eval, Capture, OpenLoop }

    /// <summary>The recording axis, parsed once at the batch boundary. Off by default; enabled records every episode (<see cref="all"/>) or the listed per-block indices. Carried into play mode as a serialized field on the spec.</summary>
    [Serializable]
    public struct RecordPlan
    {
        public bool enabled;
        public bool all;
        public int[] episodes;
        public int width;
        public int height;
        public int everyFixedSteps;

        public bool Records(int episode) =>
            enabled && (all || (episodes != null && Array.IndexOf(episodes, episode) >= 0));
    }

    public enum OpponentKind { Roster, Archetype, Mirror, Checkpoint }

    /// <summary>One block's opponent config: a pinned scripted archetype (production drive, or one K1-2 open-loop arm), the candidate's own mirror, or a second frozen checkpoint.</summary>
    public struct OpponentSpec
    {
        public const string MirrorLabel = "Mirror";

        public OpponentKind kind;
        public OpponentArchetype archetype;
        public string checkpointStem;
        public ArchetypeDrive drive;

        public static readonly OpponentSpec Mirror = new() { kind = OpponentKind.Mirror };

        public static OpponentSpec Pinned(OpponentArchetype archetype) =>
            new() { kind = OpponentKind.Archetype, archetype = archetype };

        public static OpponentSpec OpenLoop(OpponentArchetype archetype, ArchetypeDrive drive) =>
            new() { kind = OpponentKind.Archetype, archetype = archetype, drive = drive };

        public static OpponentSpec Checkpoint(string stem) =>
            new() { kind = OpponentKind.Checkpoint, checkpointStem = stem };

        public string Label => kind switch
        {
            OpponentKind.Mirror => MirrorLabel,
            OpponentKind.Checkpoint => checkpointStem,
            _ when drive == ArchetypeDrive.OpenLoopLegacy => archetype + "-legacy",
            _ when drive == ArchetypeDrive.OpenLoopAnchored => archetype + "-anchored",
            _ => archetype.ToString(),
        };
    }

    /// <summary>One selected probe with its parsed params — parallel key/value arrays because Dictionary is not Unity-serializable.</summary>
    [Serializable]
    public struct ProbeSpec
    {
        public string name;
        public string[] keys;
        public float[] values;

        public static ProbeSpec Named(string name) =>
            new() { name = name, keys = Array.Empty<string>(), values = Array.Empty<float>() };

        internal IReadOnlyDictionary<string, float> ToParameters()
        {
            if (keys == null || keys.Length == 0) return null;
            var parameters = new Dictionary<string, float>(keys.Length);
            for (var i = 0; i < keys.Length; i++) parameters[keys[i]] = values[i];
            return parameters;
        }
    }

    /// <summary>A harness session's fully-resolved configuration, parsed from the environment ONCE at the batch boundary — before play mode — so a malformed value fails there instead of inside a running episode loop. Carried into play mode as a serialized field on <see cref="HarnessSessionHost"/>.</summary>
    [Serializable]
    public sealed class SessionSpec
    {
        public const string RosterToken = "roster";
        public const string MirrorToken = "mirror";
        public const string EvalLaneToken = "eval";
        public const string CaptureLaneToken = "capture";
        public const string RecordAllToken = "all";
        public const int DefaultEpisodesPerSeed = 5;
        public const int DefaultRecordWidth = 960;
        public const int DefaultRecordHeight = 540;
        public const int DefaultRecordEvery = 5;

        public SessionLane lane;
        public string onnxAssetPath;
        public string onnxSourcePath;
        public int[] seeds;
        public string tag;
        public int episodesPerSeed;
        public float fieldDensityScale;
        public OpponentKind opponentKind;
        public OpponentArchetype opponentArchetype;
        public string opponentOnnxAssetPath;
        public string opponentOnnxSourcePath;
        public string opponentLabel;
        public ProbeSpec[] probes;
        public string[] painters;
        public RecordPlan record;
        public string outDir;
        /// <summary>OpenLoop lane only: the measured archetypes, each run as a paired legacy+anchored block pair.</summary>
        public OpponentArchetype[] openLoopArchetypes;

        /// <summary>Visuals and audio exist for this session iff it records — no separate env var until a watch/playtest lane needs one.</summary>
        public bool Presentation => record.enabled;

        private static readonly string[] RetiredNames =
        {
            "RL_EVAL_ONNX", "RL_EVAL_SEEDS", "RL_EVAL_EPISODES_PER_SEED", "RL_EVAL_DENSITY",
            "RL_EVAL_OPPONENT", "RL_EVAL_PROBES", "RL_EVAL_OUT_DIR",
        };

        /// <summary>Parses a harness session's environment. <paramref name="importCandidate"/> and <paramref name="importOpponent"/> each import a checkpoint file into their fixture slot and return its asset path (AssetDatabase work the parse itself stays free of); <paramref name="hasGraphicsDevice"/> reports whether the editor booted with a graphics device (injected so the record⇒graphics check is EditMode-testable).</summary>
        public static SessionSpec ParseEval(Func<string, string> getEnv, Func<string, string> importCandidate,
            Func<string, string> importOpponent, Func<bool> hasGraphicsDevice)
        {
            ThrowOnRetiredNames(getEnv);
            var openLoop = getEnv("RL_HARNESS_OPENLOOP");
            var source = getEnv("RL_HARNESS_ONNX");
            if (openLoop != null && source != null)
                throw new ArgumentException(
                    "RL_HARNESS_OPENLOOP measures scripted archetypes; RL_HARNESS_ONNX has no role in the open-loop lane.");
            if (openLoop != null && getEnv("RL_HARNESS_OPPONENT") != null)
                throw new ArgumentException(
                    "The measured archetype rides RL_HARNESS_OPENLOOP; RL_HARNESS_OPPONENT selects eval-lane opponents.");
            if (openLoop != null && getEnv("RL_HARNESS_LANE") != null)
                throw new ArgumentException(
                    "RL_HARNESS_OPENLOOP implies its own lane; RL_HARNESS_LANE selects the eval/capture lanes.");
            var spec = new SessionSpec
            {
                lane = openLoop != null ? SessionLane.OpenLoop : ParseLane(getEnv("RL_HARNESS_LANE")),
                onnxSourcePath = source,
                onnxAssetPath = string.IsNullOrEmpty(source)
                    ? ShipAgentFactory.SmokeFixturePath
                    : importCandidate(source),
                episodesPerSeed = ParseEpisodes(getEnv("RL_HARNESS_EPISODES_PER_SEED")),
                fieldDensityScale = ParseDensity(getEnv("RL_HARNESS_DENSITY")),
                probes = ParseProbes(getEnv("RL_HARNESS_PROBES"), openLoop != null),
                painters = ParsePainters(getEnv("RL_HARNESS_PAINTERS")),
                outDir = getEnv("RL_HARNESS_OUT_DIR"),
            };
            spec.seeds = ParseSeeds(getEnv("RL_HARNESS_SEEDS"), out var tag);
            // A non-canonical density (the 3.0 stretch) marks its artifacts so it can never pass as the canonical eval.
            spec.tag = Mathf.Approximately(spec.fieldDensityScale, EvalProtocol.CanonicalFieldDensityScale)
                ? tag
                : tag + "-d" + spec.fieldDensityScale.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_');
            if (openLoop != null)
            {
                spec.ParseOpenLoop(openLoop);
                spec.tag = "velrebase-" + spec.tag;
            }
            else
            {
                spec.ParseOpponent(getEnv("RL_HARNESS_OPPONENT"), importOpponent);
            }
            spec.record = ParseRecord(getEnv, spec.episodesPerSeed, hasGraphicsDevice);
            spec.ValidateLane();
            return spec;
        }

        private static SessionLane ParseLane(string value)
        {
            if (string.IsNullOrEmpty(value) || Matches(value, EvalLaneToken)) return SessionLane.Eval;
            if (Matches(value, CaptureLaneToken)) return SessionLane.Capture;
            throw new ArgumentException($"RL_HARNESS_LANE='{value}' is not \"{EvalLaneToken}\" or \"{CaptureLaneToken}\".");
        }

        // The capture client is the frozen "once" protocol: one seed, one opponent block. Roster stratification is an eval concept.
        private void ValidateLane()
        {
            if (lane != SessionLane.Capture) return;
            if (seeds.Length != 1)
                throw new ArgumentException(
                    $"RL_HARNESS_LANE=capture films exactly one seed; RL_HARNESS_SEEDS resolved to {seeds.Length}.");
            if (opponentKind == OpponentKind.Roster)
                throw new ArgumentException(
                    "RL_HARNESS_LANE=capture needs a single opponent block; set RL_HARNESS_OPPONENT to an archetype, "
                    + "\"mirror\", or a checkpoint path (roster stratification is an eval concept).");
        }

        /// <summary>Comma-separated painter names; default the ship-diagnostics overlay. Resolution checks the painter registry first (a probe exposing a painter under its own name — slice D — is the future second source).</summary>
        private static string[] ParsePainters(string value)
        {
            if (value == null) return new[] { DiagnosticPainters.ShipDiagnostics };
            var names = new List<string>();
            foreach (var raw in value.Split(','))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;
                if (!DiagnosticPainters.IsRegistered(name))
                    throw new ArgumentException(
                        $"RL_HARNESS_PAINTERS name '{name}' is not a registered painter: {DiagnosticPainters.RegisteredNames}.");
                names.Add(name);
            }
            return names.ToArray();
        }

        private static RecordPlan ParseRecord(Func<string, string> getEnv, int episodesPerSeed,
            Func<bool> hasGraphicsDevice)
        {
            var plan = new RecordPlan
            {
                width = DefaultRecordWidth,
                height = DefaultRecordHeight,
                everyFixedSteps = DefaultRecordEvery,
            };
            var value = getEnv("RL_HARNESS_RECORD");
            if (string.IsNullOrEmpty(value)) return plan;

            plan.enabled = true;
            if (Matches(value, RecordAllToken)) plan.all = true;
            else plan.episodes = ParseRecordIndices(value, episodesPerSeed);

            ParseRecordSize(getEnv("RL_HARNESS_RECORD_SIZE"), ref plan);
            plan.everyFixedSteps = ParseRecordEvery(getEnv("RL_HARNESS_RECORD_EVERY"));

            // Offscreen capture needs a real camera render; -nographics leaves the device Null and would film nothing.
            if (!hasGraphicsDevice())
                throw new ArgumentException(
                    "RL_HARNESS_RECORD needs a graphics device — the batch child must drop -nographics for a recording run.");
            return plan;
        }

        private static int[] ParseRecordIndices(string value, int episodesPerSeed)
        {
            var indices = new List<int>();
            foreach (var raw in value.Split(','))
            {
                var text = raw.Trim();
                if (text.Length == 0) continue;
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0)
                    throw new ArgumentException(
                        $"RL_HARNESS_RECORD='{value}': '{text}' is not \"all\" or a non-negative episode index.");
                if (index >= episodesPerSeed)
                    throw new ArgumentException(
                        $"RL_HARNESS_RECORD index {index} is out of range for {episodesPerSeed} episodes/seed.");
                if (indices.Contains(index))
                    throw new ArgumentException($"RL_HARNESS_RECORD='{value}': duplicate episode index {index}.");
                indices.Add(index);
            }
            if (indices.Count == 0)
                throw new ArgumentException(
                    $"RL_HARNESS_RECORD='{value}' selected no episodes; use \"all\" or comma-separated indices.");
            return indices.ToArray();
        }

        private static void ParseRecordSize(string value, ref RecordPlan plan)
        {
            if (string.IsNullOrEmpty(value)) return;
            var parts = value.Split('x', 'X');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
                throw new ArgumentException($"RL_HARNESS_RECORD_SIZE='{value}' is not \"WxH\".");
            if (width <= 0 || height <= 0 || width % 2 != 0 || height % 2 != 0)
                throw new ArgumentException($"RL_HARNESS_RECORD_SIZE='{value}' must be positive and even (yuv420p).");
            plan.width = width;
            plan.height = height;
        }

        private static int ParseRecordEvery(string value)
        {
            if (string.IsNullOrEmpty(value)) return DefaultRecordEvery;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var every) || every <= 0)
                throw new ArgumentException($"RL_HARNESS_RECORD_EVERY='{value}' is not a positive step cadence.");
            return every;
        }

        private static readonly OpponentArchetype[] RebasableArchetypes =
        {
            OpponentArchetype.Aggressor, OpponentArchetype.Evader, OpponentArchetype.Orbiter, OpponentArchetype.Kiter,
        };

        /// <summary>Grammar: "all" (the four velocity-law archetypes) or one archetype name; Dummy has no law to rebase.</summary>
        private void ParseOpenLoop(string token)
        {
            if (Matches(token, "all"))
            {
                openLoopArchetypes = (OpponentArchetype[])RebasableArchetypes.Clone();
                return;
            }
            if (!Enum.TryParse<OpponentArchetype>(token, ignoreCase: true, out var archetype)
                || archetype == OpponentArchetype.Dummy)
                throw new ArgumentException($"RL_HARNESS_OPENLOOP='{token}' is not \"all\" or one of "
                    + $"{string.Join(", ", RebasableArchetypes)} (Dummy has no velocity law to rebase).");
            openLoopArchetypes = new[] { archetype };
        }

        // A stale script setting a retired name would otherwise silently eval the smoke fixture.
        private static void ThrowOnRetiredNames(Func<string, string> getEnv)
        {
            foreach (var retired in RetiredNames)
                if (getEnv(retired) != null)
                    throw new ArgumentException(
                        $"{retired} is retired; set RL_HARNESS_{retired.Substring("RL_EVAL_".Length)} instead.");
        }

        private static int ParseEpisodes(string value)
        {
            if (string.IsNullOrEmpty(value)) return DefaultEpisodesPerSeed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var episodes)
                || episodes < 1)
                throw new ArgumentException($"RL_HARNESS_EPISODES_PER_SEED='{value}' is not a positive episode count.");
            return episodes;
        }

        private static float ParseDensity(string value)
        {
            if (string.IsNullOrEmpty(value)) return EvalProtocol.CanonicalFieldDensityScale;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var density))
                throw new ArgumentException($"RL_HARNESS_DENSITY='{value}' is not a number.");
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
                    $"RL_HARNESS_SEEDS='{selector}' is not \"held-out\", \"train\", or a comma-separated seed list.", inner);
            }
        }

        /// <summary>Grammar: comma-separated `name` or `name(key=value,…)` tokens — the split is paren-aware, so commas inside parens separate params, not probes. The default set is per-lane: the open-loop lane's policy-side probes would just throw.</summary>
        private static ProbeSpec[] ParseProbes(string value, bool openLoop)
        {
            if (value == null)
                return openLoop
                    ? new[] { ProbeSpec.Named(VelRebaseProbe.ProbeName) }
                    : new[]
                    {
                        ProbeSpec.Named(ArchetypeGateProbe.ProbeName),
                        ProbeSpec.Named(CombatTelemetryProbe.ProbeName),
                    };
            var entries = new List<ProbeSpec>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i <= value.Length; i++)
            {
                if (i < value.Length && value[i] == '(') depth++;
                else if (i < value.Length && value[i] == ')' && --depth < 0)
                    throw ProbeError(value, "unbalanced ')'");
                if (i < value.Length && (value[i] != ',' || depth > 0)) continue;
                if (i > start) AddProbeToken(entries, value.Substring(start, i - start));
                start = i + 1;
            }
            if (depth != 0) throw ProbeError(value, "unbalanced '('");
            // The open-loop arms carry no policy chooser; a policy-side probe would die at Begin, so fail at the boundary.
            if (openLoop)
                foreach (var entry in entries)
                    if (entry.name != VelRebaseProbe.ProbeName)
                        throw ProbeError(entry.name,
                            $"only '{VelRebaseProbe.ProbeName}' runs in the open-loop lane (no policy chooser to read)");
            return entries.ToArray();
        }

        private static void AddProbeToken(List<ProbeSpec> entries, string rawToken)
        {
            var entry = ParseProbeToken(rawToken);
            foreach (var existing in entries)
                if (existing.name == entry.name)
                    throw ProbeError(rawToken, $"duplicate probe '{entry.name}'");
            entries.Add(entry);
        }

        private static ProbeSpec ParseProbeToken(string rawToken)
        {
            var token = rawToken.Trim();
            var open = token.IndexOf('(');
            var name = (open < 0 ? token : token.Substring(0, open)).Trim();
            if (!SessionProbes.IsRegistered(name))
                throw ProbeError(rawToken, $"registered probes: {SessionProbes.RegisteredNames}");
            if (open < 0) return ProbeSpec.Named(name);
            if (token[token.Length - 1] != ')')
                throw ProbeError(rawToken, "expected 'name(key=value,…)'");
            var knownKeys = SessionProbes.KnownKeys(name);
            var keys = new List<string>();
            var values = new List<float>();
            foreach (var parameter in token.Substring(open + 1, token.Length - open - 2).Split(','))
            {
                var eq = parameter.IndexOf('=');
                var key = (eq < 0 ? parameter : parameter.Substring(0, eq)).Trim();
                if (eq < 0 || key.Length == 0)
                    throw ProbeError(rawToken, "expected 'key=value'");
                if (Array.IndexOf(knownKeys, key) < 0)
                    throw ProbeError(rawToken, $"'{name}' takes "
                        + (knownKeys.Length == 0 ? "no params" : $"keys: {string.Join(", ", knownKeys)}"));
                var valueText = parameter.Substring(eq + 1).Trim();
                if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    || !float.IsFinite(parsed))
                    throw ProbeError(rawToken, $"'{key}={valueText}' is not a finite number");
                keys.Add(key);
                values.Add(parsed);
            }
            return new ProbeSpec { name = name, keys = keys.ToArray(), values = values.ToArray() };
        }

        private static ArgumentException ProbeError(string token, string reason) =>
            new($"RL_HARNESS_PROBES token '{token.Trim()}': {reason}.");

        private void ParseOpponent(string token, Func<string, string> importOpponent)
        {
            if (string.IsNullOrEmpty(token) || Matches(token, RosterToken)) return;
            if (Matches(token, MirrorToken))
            {
                opponentKind = OpponentKind.Mirror;
                return;
            }
            if (token.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
            {
                opponentKind = OpponentKind.Checkpoint;
                opponentOnnxSourcePath = token;
                opponentOnnxAssetPath = importOpponent(token);
                opponentLabel = Path.GetFileNameWithoutExtension(token);
                return;
            }
            if (!Enum.TryParse<OpponentArchetype>(token, ignoreCase: true, out var archetype))
                throw new ArgumentException($"RL_HARNESS_OPPONENT='{token}' is not \"{RosterToken}\", \"{MirrorToken}\", "
                    + $"a checkpoint path ending .onnx, or one of {string.Join(", ", Enum.GetNames(typeof(OpponentArchetype)))}.");
            opponentKind = OpponentKind.Archetype;
            opponentArchetype = archetype;
        }

        private static bool Matches(string token, string keyword) =>
            string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);
    }
}
