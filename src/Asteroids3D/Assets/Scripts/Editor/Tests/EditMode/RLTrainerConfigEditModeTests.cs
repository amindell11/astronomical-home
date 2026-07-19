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
    /// <summary>Pins the cross-language invariants between every runnable trainer YAML and the Unity side: trainer γ must equal RewardSpec's shaping γ (Ng-shaping soundness), and engine_settings must satisfy the pacing contract (frame ≙ fixed step).</summary>
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

        [Test]
        public void ConfigFamily_CoversMainPilotAndSmoke()
        {
            var names = TrainerConfigs().Select(Path.GetFileName).ToArray();
            CollectionAssert.IsSubsetOf(
                new[] { "ppo_ship_combat.yaml", "ppo_ship_combat_pilot.yaml", "ppo_ship_combat_smoke.yaml" },
                names,
                "a renamed/deleted trainer config silently drops out of the per-file invariant tests");
        }

        [TestCaseSource(nameof(TrainerConfigs))]
        public void TrainerGamma_EqualsRewardSpecGamma(string yamlPath)
        {
            Assert.AreEqual(RewardSpec.Default.gamma, FloatValue(yamlPath, "gamma"), 1e-6f,
                "Trainer discount must equal RewardSpec.gamma — potential-based shaping is only policy-invariant at the trainer's γ");
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

        private static float LessonZeroValue(string block, string key)
        {
            var param = Regex.Match(block, $@"^  {key}:(.*)$((\r?\n(?:    .*)?)*)", RegexOptions.Multiline);
            Assert.IsTrue(param.Success, $"{key} not found under environment_parameters");
            var scalar = Regex.Match(param.Groups[1].Value, @"([^\s#]+)");
            if (scalar.Success)
                return float.Parse(scalar.Groups[1].Value, CultureInfo.InvariantCulture);
            var lessonZero = Regex.Match(param.Groups[2].Value, @"value:\s*([^\s#]+)");
            Assert.IsTrue(lessonZero.Success, $"{key} has neither a scalar value nor a curriculum lesson value");
            return float.Parse(lessonZero.Groups[1].Value, CultureInfo.InvariantCulture);
        }

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
            // Schedule starts frozen by the PR-D brief; the rest must not drift from RewardSpec.Default.
            Assert.AreEqual(1f, LessonZeroValue(block, EnvParamOverlay.UseAsteroidField), 1e-6f,
                "asteroid episodes are on for the whole curriculum run");
            Assert.AreEqual(0.25f, LessonZeroValue(block, EnvParamOverlay.CollisionLethality), 1e-6f,
                "lethality ramp starts soft (0.25 → 1.0)");
            Assert.AreEqual(0.4f, LessonZeroValue(block, EnvParamOverlay.OpponentWeightDummy), 1e-6f,
                "dummy weight starts at the curriculum floor (0.4 → 0.1)");
            Assert.AreEqual(defaults.fieldDensityScale, LessonZeroValue(block, EnvParamOverlay.FieldDensityScale), 1e-6f);
            Assert.AreEqual(defaults.weightAggressor, LessonZeroValue(block, EnvParamOverlay.OpponentWeightAggressor), 1e-6f);
            Assert.AreEqual(defaults.weightEvader, LessonZeroValue(block, EnvParamOverlay.OpponentWeightEvader), 1e-6f);
            Assert.AreEqual(defaults.weightOrbiter, LessonZeroValue(block, EnvParamOverlay.OpponentWeightOrbiter), 1e-6f);
            Assert.AreEqual(defaults.weightKiter, LessonZeroValue(block, EnvParamOverlay.OpponentWeightKiter), 1e-6f);
        }
    }
}
#endif
