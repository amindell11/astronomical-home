#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the pure env-param overlay (curriculum → RewardSpec): every parameter applies to its spec field, the 0/1 asteroid-field flag parses at 0.5, a non-positive mixture-weight sum throws at the boundary, and an absent trainer leaves the spec untouched.</summary>
    [Category("AI")]
    public class RLEnvParamOverlayEditModeTests
    {
        private static Func<string, float, float> Getter(Dictionary<string, float> values) =>
            (key, fallback) => values.TryGetValue(key, out var v) ? v : fallback;

        [Test]
        public void Apply_AllParamsPresent_OverridesEverySpecField()
        {
            var applied = EnvParamOverlay.Apply(RewardSpec.Default, Getter(new Dictionary<string, float>
            {
                [EnvParamOverlay.UseAsteroidField] = 1f,
                [EnvParamOverlay.FieldDensityScale] = 1.5f,
                [EnvParamOverlay.CollisionLethality] = 0.25f,
                [EnvParamOverlay.OpponentWeightAggressor] = 0.5f,
                [EnvParamOverlay.OpponentWeightEvader] = 0.3f,
                [EnvParamOverlay.OpponentWeightOrbiter] = 0.11f,
                [EnvParamOverlay.OpponentWeightKiter] = 0.07f,
                [EnvParamOverlay.OpponentWeightDummy] = 0.02f,
            }));

            Assert.IsTrue(applied.useAsteroidField);
            Assert.AreEqual(1.5f, applied.fieldDensityScale);
            Assert.AreEqual(0.25f, applied.collisionLethality);
            Assert.AreEqual(0.5f, applied.weightAggressor);
            Assert.AreEqual(0.3f, applied.weightEvader);
            Assert.AreEqual(0.11f, applied.weightOrbiter);
            Assert.AreEqual(0.07f, applied.weightKiter);
            Assert.AreEqual(0.02f, applied.weightDummy);
        }

        [Test]
        public void Apply_UseAsteroidField_ParsesAsHalfThreshold()
        {
            Assert.IsFalse(EnvParamOverlay.Apply(RewardSpec.Default,
                Getter(new Dictionary<string, float> { [EnvParamOverlay.UseAsteroidField] = 0f })).useAsteroidField);
            Assert.IsFalse(EnvParamOverlay.Apply(RewardSpec.Default,
                Getter(new Dictionary<string, float> { [EnvParamOverlay.UseAsteroidField] = 0.4f })).useAsteroidField);
            Assert.IsTrue(EnvParamOverlay.Apply(RewardSpec.Default,
                Getter(new Dictionary<string, float> { [EnvParamOverlay.UseAsteroidField] = 0.6f })).useAsteroidField);
            Assert.IsTrue(EnvParamOverlay.Apply(RewardSpec.Default,
                Getter(new Dictionary<string, float> { [EnvParamOverlay.UseAsteroidField] = 1f })).useAsteroidField);
        }

        [Test]
        public void Apply_NonPositiveWeightSum_Throws()
        {
            var zeroed = new Dictionary<string, float>
            {
                [EnvParamOverlay.OpponentWeightAggressor] = 0f,
                [EnvParamOverlay.OpponentWeightEvader] = 0f,
                [EnvParamOverlay.OpponentWeightOrbiter] = 0f,
                [EnvParamOverlay.OpponentWeightKiter] = 0f,
                [EnvParamOverlay.OpponentWeightDummy] = 0f,
            };
            Assert.Throws<InvalidOperationException>(() => EnvParamOverlay.Apply(RewardSpec.Default, Getter(zeroed)));

            zeroed[EnvParamOverlay.OpponentWeightDummy] = -1f;
            Assert.Throws<InvalidOperationException>(() => EnvParamOverlay.Apply(RewardSpec.Default, Getter(zeroed)));
        }

        [Test]
        public void Apply_NoTrainerParams_LeavesTheSpecUntouched()
        {
            Assert.AreEqual(RewardSpec.Default,
                EnvParamOverlay.Apply(RewardSpec.Default, Getter(new Dictionary<string, float>())));
        }
    }
}
#endif
