#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.RLHarness;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the cross-language invariants between every runnable trainer YAML and the Unity side: trainer γ must equal RewardSpec.gamma (the reward scale is calibrated against it; shaping telescopes undiscounted), and engine_settings must satisfy the pacing contract (frame ≙ fixed step).</summary>
    [Category("AI")]
    public class RLTrainerConfigEditModeTests
    {
        private static IEnumerable<string> TrainerConfigs()
        {
            var dir = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "training", "rl"));
            var configs = Directory.GetFiles(dir, "*.yaml").OrderBy(p => p).ToArray();
            Assert.IsNotEmpty(configs, $"no trainer configs found under {dir}");
            return configs;
        }

        private static string Value(string yamlPath, string key)
        {
            var yaml = File.ReadAllText(yamlPath);
            var match = Regex.Match(yaml, $@"^\s*{key}:\s*([^\s#]+)", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, $"{key} not found in {yamlPath}");
            return match.Groups[1].Value;
        }

        private static float FloatValue(string yamlPath, string key) =>
            float.Parse(Value(yamlPath, key), CultureInfo.InvariantCulture);

        private const string ReferenceConfig = "ppo_ship_combat.yaml";

        private static readonly string[] FamilyConfigs =
        {
            ReferenceConfig, "ppo_ship_combat_pilot.yaml", "ppo_ship_combat_smoke.yaml",
            "ppo_ship_combat_hybrid.yaml", "ppo_ship_combat_selfplay.yaml", "ppo_ship_combat_selfplay_smoke.yaml",
        };

        /// <summary>Algorithm shape: every run in the family must share these, so runs stay rankable against each other.</summary>
        private static readonly string[] SharedHyperparameters =
        {
            "learning_rate", "beta", "epsilon", "lambd", "num_epoch", "learning_rate_schedule", "beta_schedule",
        };

        /// <summary>Sample budget: the smokes deliberately shrink these; every real run must not.</summary>
        private static readonly string[] FullRunHyperparameters = { "batch_size", "buffer_size" };

        private static bool IsSmoke(string yamlPath) => Path.GetFileName(yamlPath).Contains("smoke");

        [Test]
        public void ConfigFamily_CoversEveryRunnableConfig()
        {
            var names = TrainerConfigs().Select(Path.GetFileName).ToArray();
            CollectionAssert.IsSubsetOf(FamilyConfigs, names,
                "a renamed/deleted trainer config silently drops out of the per-file invariant tests");
        }

        // Hybrid ran lambd 0.95 against the main config's 0.98 for a whole 1.5M-step phase before anyone
        // noticed, which makes its gate series unrankable against the run it warm-started from.
        [TestCaseSource(nameof(TrainerConfigs))]
        public void Hyperparameters_MatchTheFamilyRecipe(string yamlPath)
        {
            var reference = TrainerConfigs().Single(p => Path.GetFileName(p) == ReferenceConfig);
            foreach (var key in SharedHyperparameters)
                Assert.AreEqual(Value(reference, key), Value(yamlPath, key),
                    $"{key} drifted from {ReferenceConfig} — a run on a different algorithm shape cannot be compared to any other run");

            if (IsSmoke(yamlPath)) return;
            foreach (var key in FullRunHyperparameters)
                Assert.AreEqual(Value(reference, key), Value(yamlPath, key),
                    $"{key} drifted from {ReferenceConfig}; only the smokes may shrink the sample budget");
        }

        [TestCaseSource(nameof(TrainerConfigs))]
        public void TrainerGamma_EqualsRewardSpecGamma(string yamlPath)
        {
            Assert.AreEqual(RewardSpec.Default.gamma, FloatValue(yamlPath, "gamma"), 1e-6f,
                "Trainer discount must equal RewardSpec.gamma — the reward scale was calibrated against this γ (shaping itself telescopes undiscounted, see PotentialShaping.Step)");
        }

        [TestCaseSource(nameof(TrainerConfigs))]
        public void EngineSettings_SatisfyThePacingContract(string yamlPath)
        {
            Assert.AreEqual(1f, FloatValue(yamlPath, "time_scale"), 1e-6f);
            Assert.AreEqual(Mathf.RoundToInt(1f / Time.fixedDeltaTime),
                int.Parse(Value(yamlPath, "capture_frame_rate"), CultureInfo.InvariantCulture),
                "capture_frame_rate × time_scale must advance exactly one fixed step per rendered frame");
        }

        private static string EnvironmentParametersBlock()
        {
            var mainConfig = TrainerConfigs().Single(p => Path.GetFileName(p) == "ppo_ship_combat.yaml");
            var lines = File.ReadAllLines(mainConfig);
            var start = System.Array.IndexOf(lines, "environment_parameters:");
            Assert.GreaterOrEqual(start, 0, "ppo_ship_combat.yaml must carry the curriculum's environment_parameters block");
            var block = new System.Text.StringBuilder();
            for (var i = start + 1; i < lines.Length && (lines[i].Length == 0 || lines[i][0] == ' ' || lines[i][0] == '#'); i++)
                if (lines[i].Length > 0 && lines[i][0] == ' ')
                    block.AppendLine(lines[i]);
            return block.ToString();
        }

        private static float LessonValue(string block, string key, bool final)
        {
            var param = Regex.Match(block, $@"^  {key}:(.*)$((\r?\n(?:    .*)?)*)", RegexOptions.Multiline);
            Assert.IsTrue(param.Success, $"{key} not found under environment_parameters");
            var scalar = Regex.Match(param.Groups[1].Value, @"([^\s#]+)");
            if (scalar.Success)
                return float.Parse(scalar.Groups[1].Value, CultureInfo.InvariantCulture);
            var lessons = Regex.Matches(param.Groups[2].Value, @"value:\s*([^\s#]+)");
            Assert.Greater(lessons.Count, 0, $"{key} has neither a scalar value nor a curriculum lesson value");
            return float.Parse(lessons[final ? lessons.Count - 1 : 0].Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static float LessonZeroValue(string block, string key) => LessonValue(block, key, final: false);

        private static float LessonFinalValue(string block, string key) => LessonValue(block, key, final: true);

        [Test]
        public void EnvironmentParameters_KeysMatchOverlayConsts()
        {
            var keys = Regex.Matches(EnvironmentParametersBlock(), @"^  (\w+):", RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
            CollectionAssert.AreEquivalent(EnvParamOverlay.ParamNames, keys,
                "a key that drifts from EnvParamOverlay's consts makes GetWithDefault fall back silently — the curriculum would not apply");
        }

        [Test]
        public void EnvironmentParameters_LessonZeroValues_MatchSpecDefaultsAndScheduleStarts()
        {
            var block = EnvironmentParametersBlock();
            var defaults = RewardSpec.Default;
            // Curriculum ramps deliberately start off-default; everything else must not drift from RewardSpec.Default.
            Assert.AreEqual(1f, LessonZeroValue(block, EnvParamOverlay.UseAsteroidField), 1e-6f,
                "the field flag is constant — composition is boot-frozen, so the early phase is low density (0.1-0.3), never a flag flip");
            Assert.AreEqual(0.25f, LessonZeroValue(block, EnvParamOverlay.CollisionLethality), 1e-6f,
                "lethality ramp starts soft (0.25 → 1.0)");
            Assert.AreEqual(8f, LessonZeroValue(block, EnvParamOverlay.OpponentWeightDummy), 1e-6f,
                "dummy weight starts at the ignition lesson (~90% Dummy under sum-normalization; 8.0 → 0.1)");
            Assert.AreEqual(defaults.weightAggressor, LessonZeroValue(block, EnvParamOverlay.OpponentWeightAggressor), 1e-6f);
            Assert.AreEqual(defaults.weightEvader, LessonZeroValue(block, EnvParamOverlay.OpponentWeightEvader), 1e-6f);
            Assert.AreEqual(defaults.weightOrbiter, LessonZeroValue(block, EnvParamOverlay.OpponentWeightOrbiter), 1e-6f);
            Assert.AreEqual(defaults.weightKiter, LessonZeroValue(block, EnvParamOverlay.OpponentWeightKiter), 1e-6f);
        }

        [Test]
        public void SelfPlayConfig_SelfPlayBlock_TerminalBandGraduationEnv_NoRoster()
        {
            var path = TrainerConfigs().Single(p => Path.GetFileName(p) == "ppo_ship_combat_selfplay.yaml");
            var yaml = File.ReadAllText(path);

            Assert.IsTrue(Regex.IsMatch(yaml, @"^\s*self_play:", RegexOptions.Multiline),
                "the self-play run must carry a self_play block");
            foreach (var key in new[] { "save_steps", "team_change", "swap_steps", "window",
                "play_against_latest_model_ratio", "initial_elo" })
                Assert.IsTrue(Regex.IsMatch(yaml, $@"^\s*{key}:", RegexOptions.Multiline),
                    $"self_play block missing {key}");

            Assert.AreEqual(RewardSpec.Default.gamma, FloatValue(path, "gamma"), 1e-6f,
                "self-play trainer γ must equal RewardSpec.gamma (same reward calibration as the scripted-roster run)");

            Assert.AreEqual(1f, FloatValue(path, "use_asteroid_field"), 1e-6f);
            Assert.AreEqual(1f, FloatValue(path, "collision_lethality"), 1e-6f);

            AssertTerminalDensityBandSampler(yaml);
            StringAssert.DoesNotContain("curriculum:", yaml,
                "self-play graduates at the terminal band as a sampler — never a ramp that moves under the league");
            Assert.IsFalse(Regex.IsMatch(yaml, @"^\s*opponent_weight_", RegexOptions.Multiline),
                "the scripted roster is unused in self-play — no opponent_weight_ params");
        }

        [Test]
        public void HybridConfig_SelfPlayBlock_TerminalBand_EvaderDominantRoster()
        {
            var path = TrainerConfigs().Single(p => Path.GetFileName(p) == "ppo_ship_combat_hybrid.yaml");
            var yaml = File.ReadAllText(path);

            Assert.IsTrue(Regex.IsMatch(yaml, @"^\s*self_play:", RegexOptions.Multiline),
                "the hybrid run is still a self-play run — the mirror workers need the ghost league");
            Assert.AreEqual(RewardSpec.Default.gamma, FloatValue(path, "gamma"), 1e-6f,
                "hybrid trainer γ must equal RewardSpec.gamma (same reward calibration as every other run)");

            AssertTerminalDensityBandSampler(yaml);
            Assert.AreEqual(1f, FloatValue(path, "collision_lethality"), 1e-6f);

            Assert.Greater(FloatValue(path, "team_change"), FloatValue(path, "max_steps"),
                "team_change must outlast the run — scripted workers are team-0-only, so a learner swap ghosts them");

            // Only the scripted workers read the roster mix; Evader dominance is the split's whole point.
            var evader = FloatValue(path, EnvParamOverlay.OpponentWeightEvader);
            foreach (var key in new[] { EnvParamOverlay.OpponentWeightAggressor, EnvParamOverlay.OpponentWeightOrbiter,
                EnvParamOverlay.OpponentWeightKiter, EnvParamOverlay.OpponentWeightDummy })
                Assert.Greater(evader, FloatValue(path, key),
                    $"Evader must dominate {key} — pursuit is the capability the scripted workers protect");
        }

        [Test]
        public void FieldDensity_UniformSamplerRange_ContainsTheCanonicalEvalDensity()
        {
            var param = Regex.Match(EnvironmentParametersBlock(),
                $@"^  {EnvParamOverlay.FieldDensityScale}:.*$((\r?\n(?:    .*)?)*)", RegexOptions.Multiline);
            Assert.IsTrue(param.Success, "field_density_scale not found under environment_parameters");
            StringAssert.Contains("sampler_type: uniform", param.Value,
                "density trains as per-episode uniform sampler bands, ramped by curriculum lesson");
            var min = SamplerBound(param.Value, "min_value", final: true);
            var max = SamplerBound(param.Value, "max_value", final: true);
            Assert.AreEqual(0.5f, min, 1e-6f);
            Assert.AreEqual(2.5f, max, 1e-6f);
            Assert.That(EvalProtocol.CanonicalFieldDensityScale, Is.InRange(min, max),
                "checkpoint eval must run inside the terminal density band training samples from — an out-of-band eval env invalidates every selection");
        }

        // Density samples the terminal band per episode — graduating at one density overfits it —
        // but the band must never reach 0: an empty obstacle BufferSensor collapses the policy.
        private static void AssertTerminalDensityBandSampler(string yaml)
        {
            var density = Regex.Match(yaml, @"^  field_density_scale:.*$((\r?\n(?:    .*)?)*)", RegexOptions.Multiline);
            Assert.IsTrue(density.Success, "field_density_scale not found under environment_parameters");
            StringAssert.Contains("sampler_type: uniform", density.Value);
            var min = SamplerBound(density.Value, "min_value");
            Assert.AreEqual(0.5f, min, 1e-6f);
            Assert.AreEqual(2.5f, SamplerBound(density.Value, "max_value"), 1e-6f);
            Assert.Greater(min, 0f, "a zero-density episode leaves the attention buffer empty");
        }

        private static float SamplerBound(string samplerBlock, string key, bool final = false)
        {
            var matches = Regex.Matches(samplerBlock, $@"{key}:\s*([^\s#]+)");
            Assert.Greater(matches.Count, 0, $"{key} not found in the sampler block");
            return float.Parse(matches[final ? matches.Count - 1 : 0].Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Test]
        public void CanonicalEvalEnv_MatchesCurriculumTerminalLesson()
        {
            var block = EnvironmentParametersBlock();
            var evalSpec = EvalProtocol.EvalSpec(EvalProtocol.CanonicalFieldDensityScale);
            Assert.IsTrue(evalSpec.useAsteroidField);
            Assert.AreEqual(1f, LessonFinalValue(block, EnvParamOverlay.UseAsteroidField), 1e-6f,
                "eval runs field-on; the YAML must keep the field on through the terminal lesson");
            Assert.AreEqual(LessonFinalValue(block, EnvParamOverlay.CollisionLethality),
                evalSpec.collisionLethality, 1e-6f,
                "eval inherits RewardSpec.Default lethality — it must equal the ramp's terminal lesson");
        }
    }
}
#endif
