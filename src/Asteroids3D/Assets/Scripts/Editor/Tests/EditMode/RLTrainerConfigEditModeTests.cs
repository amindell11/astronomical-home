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
    }
}
#endif
