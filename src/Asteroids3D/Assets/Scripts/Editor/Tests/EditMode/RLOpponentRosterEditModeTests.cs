#if UNITY_EDITOR
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the roster's spec-weighted archetype selection: degenerate weights always pick their archetype, the default mixture matches its proportions over many draws, and Pick consumes exactly one roll (the burn contract keeping mixture and pinned jitter draws aligned).</summary>
    [Category("AI")]
    public class RLOpponentRosterEditModeTests
    {
        private static RewardSpec SoleWeight(OpponentArchetype archetype)
        {
            var spec = RewardSpec.Default;
            spec.weightAggressor = archetype == OpponentArchetype.Aggressor ? 1f : 0f;
            spec.weightEvader = archetype == OpponentArchetype.Evader ? 1f : 0f;
            spec.weightOrbiter = archetype == OpponentArchetype.Orbiter ? 1f : 0f;
            spec.weightKiter = archetype == OpponentArchetype.Kiter ? 1f : 0f;
            spec.weightDummy = archetype == OpponentArchetype.Dummy ? 1f : 0f;
            return spec;
        }

        [TestCase(OpponentArchetype.Aggressor)]
        [TestCase(OpponentArchetype.Evader)]
        [TestCase(OpponentArchetype.Orbiter)]
        [TestCase(OpponentArchetype.Kiter)]
        [TestCase(OpponentArchetype.Dummy)]
        public void Pick_SoleWeight_AlwaysSelectsThatArchetype(OpponentArchetype archetype)
        {
            var spec = SoleWeight(archetype);
            var rng = new System.Random(7);
            for (var i = 0; i < 200; i++)
                Assert.AreEqual(archetype, OpponentRoster.Pick(rng, in spec));
        }

        [Test]
        public void Pick_DefaultWeights_MatchesMixtureProportions()
        {
            var spec = RewardSpec.Default;
            var rng = new System.Random(11);
            const int draws = 20000;
            var counts = new int[5];
            for (var i = 0; i < draws; i++)
                counts[(int)OpponentRoster.Pick(rng, in spec)]++;

            Assert.AreEqual(spec.weightAggressor, counts[(int)OpponentArchetype.Aggressor] / (float)draws, 0.02f);
            Assert.AreEqual(spec.weightEvader, counts[(int)OpponentArchetype.Evader] / (float)draws, 0.02f);
            Assert.AreEqual(spec.weightOrbiter, counts[(int)OpponentArchetype.Orbiter] / (float)draws, 0.02f);
            Assert.AreEqual(spec.weightKiter, counts[(int)OpponentArchetype.Kiter] / (float)draws, 0.02f);
            Assert.AreEqual(spec.weightDummy, counts[(int)OpponentArchetype.Dummy] / (float)draws, 0.02f);
        }

        [Test]
        public void Pick_ConsumesExactlyOneRoll()
        {
            var spec = RewardSpec.Default;
            const int seed = 12345;
            var picked = new System.Random(seed);
            OpponentRoster.Pick(picked, in spec);
            var reference = new System.Random(seed);
            reference.NextDouble();
            Assert.AreEqual(reference.NextDouble(), picked.NextDouble(),
                "burn-the-selection-roll alignment: mixture and pinned installs must leave the rng in the same state");
        }
    }
}
#endif
