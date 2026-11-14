using Capture.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AI;
using System.Text.RegularExpressions;
using Combat.Weapons;
using Ships;
using Ships.Loadout;
using RL.Opponents;
using RL.Probes;

namespace RL.Hosts
{
    /// <summary>Which lane client the host runs; every other axis of a harness run is a spec field.</summary>
    public enum HarnessLane { Matrix }

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

    /// <summary>One block's opponent label: the matchup the block names, else the archetype's name.</summary>
    public struct OpponentSpec
    {
        public OpponentArchetype archetype;
        public string labelOverride;

        public string Label => !string.IsNullOrEmpty(labelOverride) ? labelOverride : archetype.ToString();
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

    /// <summary>A harness run's fully-resolved configuration, parsed from the environment ONCE at the batch boundary — before play mode — so a malformed value fails there instead of inside a running episode loop. Carried into play mode as a serialized field on <see cref="HarnessHost"/>.</summary>
    [Serializable]
    public sealed class HarnessSpec
    {
        public const string MatrixLaneToken = "matrix";
        public const string RecordAllToken = "all";
        public const int DefaultEpisodesPerSeed = 5;
        public const int DefaultRecordWidth = 960;
        public const int DefaultRecordHeight = 540;
        public const int DefaultRecordEvery = 5;

        public HarnessLane lane;
        public int[] seeds;
        public string tag;
        public int episodesPerSeed;
        public ProbeSpec[] probes;
        public GizmoCaptureProfile gizmoProfile;
        public RecordPlan record;
        public string captureCommit;
        public string outDir;
        /// <summary>Weapon-pair mode: the weapon prefabs. Each unordered pair, a weapon against itself included, is one block.</summary>
        public WeaponComponent[] matrixWeapons;
        /// <summary>Roster mode: the fixed side. Weapon-pair mode leaves it unset.</summary>
        public MatrixSide matrixReference;
        /// <summary>Roster mode: one block per entry, fought as side B against <see cref="matrixReference"/>.</summary>
        public MatrixSide[] matrixRoster;
        /// <summary>Roster mode: both commanders hold the keystone off.</summary>
        public bool matrixKitsOff;

        /// <summary>Visuals and audio exist iff the session records plain footage — profile rule: GizmoCaptureProfiles.PresentationFor.</summary>
        public bool Presentation => record.enabled && GizmoCaptureProfiles.PresentationFor(gizmoProfile);

        // Each retired name carries its replacement, or null when it was removed with no successor.
        private static readonly (string name, string replacement)[] RetiredNames =
        {
            ("RL_EVAL_SEEDS", "RL_HARNESS_SEEDS"),
            ("RL_EVAL_EPISODES_PER_SEED", "RL_HARNESS_EPISODES_PER_SEED"),
            ("RL_EVAL_PROBES", "RL_HARNESS_PROBES"),
            ("RL_EVAL_OUT_DIR", "RL_HARNESS_OUT_DIR"),
            ("RL_HARNESS_PAINTERS", null),
        };

        // The trained-agent lanes' variables: a stale script setting one must not run a matrix in its place.
        private static readonly string[] RemovedWithRl =
        {
            "RL_EVAL_ONNX", "RL_EVAL_DENSITY", "RL_EVAL_OPPONENT",
            "RL_HARNESS_ONNX", "RL_HARNESS_OPPONENT", "RL_HARNESS_DENSITY", "RL_HARNESS_BUNDLE",
            "RL_HARNESS_SENTENCE", "RL_HARNESS_DUEL",
        };

        private static readonly string[] RemovedLanes = { "eval", "capture", "sentence", "duel" };

        // Graphics detection is injected for tests.
        public static HarnessSpec Parse(Func<string, string> getEnv, Func<IReadOnlyList<WeaponComponent>> catalogWeapons,
            Func<IReadOnlyList<Ship>> catalogChassis, Func<IReadOnlyList<AICommander>> catalogPilots,
            Func<IReadOnlyList<ShieldModule>> catalogShields, Func<bool> hasGraphicsDevice)
        {
            ThrowOnRetiredNames(getEnv);
            var lane = ParseLane(getEnv("RL_HARNESS_LANE"));
            var matrix = getEnv("RL_HARNESS_MATRIX");
            var matrixReference = getEnv("RL_HARNESS_MATRIX_REFERENCE");
            var matrixRoster = getEnv("RL_HARNESS_MATRIX_ROSTER");
            var matrixKits = getEnv("RL_HARNESS_MATRIX_KITS");
            var rosterMatrix = matrixReference != null || matrixRoster != null;
            if (rosterMatrix && matrix != null)
                throw new ArgumentException(
                    $"{(matrixReference != null ? "RL_HARNESS_MATRIX_REFERENCE" : "RL_HARNESS_MATRIX_ROSTER")} and RL_HARNESS_MATRIX select different matrix modes; set one.");
            if (matrixKits != null)
                ThrowIfMatrixKits(matrixKits, matrix, matrixReference, matrixRoster);
            if (matrix == null && !rosterMatrix)
                throw new ArgumentException(
                    "The matrix lane needs RL_HARNESS_MATRIX, or RL_HARNESS_MATRIX_REFERENCE with RL_HARNESS_MATRIX_ROSTER.");
            var spec = new HarnessSpec
            {
                lane = lane,
                episodesPerSeed = ParseEpisodes(getEnv("RL_HARNESS_EPISODES_PER_SEED")),
                probes = ParseProbes(getEnv("RL_HARNESS_PROBES")),
                gizmoProfile = ParseGizmoProfile(getEnv("RL_HARNESS_GIZMOS")),
                outDir = getEnv("RL_HARNESS_OUT_DIR"),
            };
            spec.seeds = ParseSeeds(getEnv("RL_HARNESS_SEEDS"), out var tag);
            if (matrix != null)
            {
                spec.matrixWeapons = ParseWeapons("RL_HARNESS_MATRIX", matrix, catalogWeapons());
                spec.tag = "matrix-" + tag;
            }
            else
            {
                if (matrixReference == null || matrixRoster == null)
                    throw new ArgumentException(
                        "RL_HARNESS_MATRIX_REFERENCE and RL_HARNESS_MATRIX_ROSTER select a roster matrix together; set both.");
                var chassis = catalogChassis();
                var pilots = catalogPilots();
                var weapons = catalogWeapons();
                var shields = catalogShields();
                spec.matrixReference = ParseSide("RL_HARNESS_MATRIX_REFERENCE", matrixReference.Trim(),
                    chassis, pilots, weapons, shields);
                spec.matrixRoster = ParseRoster(matrixRoster, chassis, pilots, weapons, shields);
                spec.matrixKitsOff = matrixKits != null;
                spec.tag = (spec.matrixKitsOff ? "matrix-kitsoff-" : "matrix-") + tag;
            }
            spec.record = ParseRecord(getEnv, spec.episodesPerSeed, hasGraphicsDevice);
            if (spec.record.enabled)
            {
                spec.captureCommit = getEnv("RL_HARNESS_CAPTURE_COMMIT");
                if (spec.captureCommit == null || !Regex.IsMatch(spec.captureCommit, "^[0-9a-fA-F]{40}$"))
                    throw new ArgumentException("RL_HARNESS_CAPTURE_COMMIT must name the launcher's 40-character hexadecimal SHA when recording.");
            }
            spec.ValidateCaptureSelector();
            return spec;
        }

        private static HarnessLane ParseLane(string value)
        {
            if (string.IsNullOrEmpty(value) || Matches(value, MatrixLaneToken)) return HarnessLane.Matrix;
            if (RemovedLanes.Any(removed => Matches(value, removed)))
                throw new ArgumentException(
                    $"RL_HARNESS_LANE='{value}': that lane was removed with the RL training stack; only \"{MatrixLaneToken}\" remains.");
            throw new ArgumentException($"RL_HARNESS_LANE='{value}' is not \"{MatrixLaneToken}\".");
        }

        private static GizmoCaptureProfile ParseGizmoProfile(string value)
        {
            if (string.IsNullOrEmpty(value)) return GizmoCaptureProfile.None;
            if (Matches(value, "steering")) return GizmoCaptureProfile.Steering;
            if (Matches(value, "combat")) return GizmoCaptureProfile.Combat;
            if (Matches(value, "everything")) return GizmoCaptureProfile.Everything;
            throw new ArgumentException(
                $"RL_HARNESS_GIZMOS='{value}' is not a capture profile (steering, combat, everything).");
        }

        private void ValidateCaptureSelector()
        {
            if (gizmoProfile == GizmoCaptureProfile.None) return;
            if (!record.enabled)
                throw new ArgumentException("RL_HARNESS_GIZMOS needs RL_HARNESS_RECORD to select filmed episodes.");
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

        /// <summary>Grammar: "all" (the item catalog's weapons, in catalog order) or comma-separated distinct catalog weapon names.</summary>
        private static WeaponComponent[] ParseWeapons(string variable, string token,
            IReadOnlyList<WeaponComponent> catalogWeapons)
        {
            if (Matches(token, "all")) return catalogWeapons.ToArray();
            var weapons = new List<WeaponComponent>();
            foreach (var raw in token.Split(','))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;
                var weapon = catalogWeapons.FirstOrDefault(w => Matches(w.name, name));
                if (!weapon)
                    throw new ArgumentException(
                        $"{variable}='{token}': '{name}' is not \"all\" or one of {string.Join(", ", catalogWeapons.Select(w => w.name))}.");
                if (weapons.Contains(weapon))
                    throw new ArgumentException($"{variable}='{token}': duplicate weapon '{name}'.");
                weapons.Add(weapon);
            }
            if (weapons.Count == 0)
                throw new ArgumentException(
                    $"{variable}='{token}' selected no weapons; use \"all\" or comma-separated weapon names.");
            return weapons.ToArray();
        }

        private static MatrixSide ParseSide(string variable, string token, IReadOnlyList<Ship> chassis,
            IReadOnlyList<AICommander> pilots, IReadOnlyList<WeaponComponent> weapons,
            IReadOnlyList<ShieldModule> shields)
        {
            var parts = token.Split(':');
            if (parts.Length < 3 || parts.Length > 4)
                throw new ArgumentException($"{variable}='{token}' is not Chassis:Pilot:Weapon[:Shield].");
            var side = new MatrixSide
            {
                chassis = ResolveNamed(variable, token, parts[0].Trim(), chassis),
                pilot = ResolveNamed(variable, token, parts[1].Trim(), pilots),
                weapon = ResolveNamed(variable, token, parts[2].Trim(), weapons),
            };
            if (parts.Length == 4)
                side.shield = ResolveNamed(variable, token, parts[3].Trim(), shields);
            return side;
        }

        private static MatrixSide[] ParseRoster(string token, IReadOnlyList<Ship> chassis,
            IReadOnlyList<AICommander> pilots, IReadOnlyList<WeaponComponent> weapons,
            IReadOnlyList<ShieldModule> shields)
        {
            const string variable = "RL_HARNESS_MATRIX_ROSTER";
            var sides = new List<MatrixSide>();
            foreach (var raw in token.Split(','))
            {
                var piece = raw.Trim();
                if (piece.Length == 0) continue;
                var side = ParseSide(variable, piece, chassis, pilots, weapons, shields);
                if (sides.Exists(existing => SameSide(existing, side)))
                    throw new ArgumentException($"{variable}='{token}': duplicate side '{piece}'.");
                sides.Add(side);
            }
            if (sides.Count == 0)
                throw new ArgumentException(
                    $"{variable}='{token}' selected no ships; use comma-separated Chassis:Pilot:Weapon[:Shield] tokens.");
            return sides.ToArray();
        }

        private static T ResolveNamed<T>(string variable, string token, string name, IReadOnlyList<T> catalog)
            where T : UnityEngine.Object
        {
            var found = catalog.FirstOrDefault(item => Matches(item.name, name));
            if (!found)
                throw new ArgumentException(
                    $"{variable}='{token}': '{name}' is not one of {string.Join(", ", catalog.Select(item => item.name))}.");
            return found;
        }

        private static bool SameSide(MatrixSide a, MatrixSide b) =>
            a.chassis == b.chassis && a.pilot == b.pilot && a.weapon == b.weapon && a.shield == b.shield;

        private static void ThrowIfMatrixKits(string value, string matrix, string reference, string roster)
        {
            const string which = "RL_HARNESS_MATRIX_KITS";
            if (!Matches(value, "off"))
                throw new ArgumentException($"{which}='{value}' is not \"off\".");
            if (matrix != null)
                throw new ArgumentException($"{which} and RL_HARNESS_MATRIX select different matrix modes; set one.");
            if (reference == null || roster == null)
                throw new ArgumentException(
                    $"{which} is valid only with RL_HARNESS_MATRIX_REFERENCE and RL_HARNESS_MATRIX_ROSTER.");
        }

        // A stale script setting a retired name would otherwise silently reshape the session.
        private static void ThrowOnRetiredNames(Func<string, string> getEnv)
        {
            foreach (var (name, replacement) in RetiredNames)
                if (getEnv(name) != null)
                    throw new ArgumentException(replacement != null
                        ? $"{name} is retired; set {replacement} instead."
                        : $"{name} is retired and removed with no replacement.");
            foreach (var name in RemovedWithRl)
                if (getEnv(name) != null)
                    throw new ArgumentException(
                        $"{name} was removed with the RL training stack; only the matrix lane remains — unset it.");
        }

        private static int ParseEpisodes(string value)
        {
            if (string.IsNullOrEmpty(value)) return DefaultEpisodesPerSeed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var episodes)
                || episodes < 1)
                throw new ArgumentException($"RL_HARNESS_EPISODES_PER_SEED='{value}' is not a positive episode count.");
            return episodes;
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

        /// <summary>Grammar: comma-separated `name` or `name(key=value,…)` tokens — the split is paren-aware, so commas inside parens separate params, not probes. Unset selects the matrix probe.</summary>
        private static ProbeSpec[] ParseProbes(string value)
        {
            if (value == null) return new[] { ProbeSpec.Named(MatrixProbe.ProbeName) };
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
            if (!HarnessProbes.IsRegistered(name))
                throw ProbeError(rawToken, $"registered probes: {HarnessProbes.RegisteredNames}");
            if (open < 0) return ProbeSpec.Named(name);
            if (token[token.Length - 1] != ')')
                throw ProbeError(rawToken, "expected 'name(key=value,…)'");
            var knownKeys = HarnessProbes.KnownKeys(name);
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

        private static bool Matches(string token, string keyword) =>
            string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);
    }
}
