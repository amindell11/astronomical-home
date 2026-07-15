#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using Game.RLHarness;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the cross-language invariants between the trainer YAML and the Unity side: trainer γ must equal RewardSpec's shaping γ (Ng-shaping soundness), and engine_settings must satisfy the pacing contract (frame ≙ fixed step).</summary>
    [Category("AI")]
    public class RLTrainerConfigEditModeTests
    {
        private static string YamlPath => Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "..", "..", "training", "rl", "ppo_ship_combat.yaml"));

        private static string Value(string yaml, string key)
        {
            var match = Regex.Match(yaml, $@"^\s*{key}:\s*([^\s#]+)", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, $"{key} not found in {YamlPath}");
            return match.Groups[1].Value;
        }

        [Test]
        public void TrainerGamma_EqualsRewardSpecGamma()
        {
            var yaml = File.ReadAllText(YamlPath);
            Assert.AreEqual(RewardSpec.Default.gamma, float.Parse(Value(yaml, "gamma")), 1e-6f,
                "Trainer discount must equal RewardSpec.gamma — potential-based shaping is only policy-invariant at the trainer's γ");
        }

        [Test]
        public void EngineSettings_SatisfyThePacingContract()
        {
            var yaml = File.ReadAllText(YamlPath);
            Assert.AreEqual(1f, float.Parse(Value(yaml, "time_scale")), 1e-6f);
            Assert.AreEqual(Mathf.RoundToInt(1f / Time.fixedDeltaTime),
                int.Parse(Value(yaml, "capture_frame_rate")),
                "capture_frame_rate × time_scale must advance exactly one fixed step per rendered frame");
        }
    }
}
#endif
