#if UNITY_EDITOR
using System.Linq;
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the frozen eval protocol: held-out seed set shape/disjointness, seed-selection resolution (train vs held-out vs explicit), and the Wilson 95% lower-bound math the arc gate reads.</summary>
    [Category("AI")]
    public class RLEvalProtocolEditModeTests
    {
        [Test]
        public void ResolveSeeds_DefaultsToHeldOutAndDistinguishesTags()
        {
            Assert.AreEqual(EvalProtocol.HeldOutSeeds, EvalProtocol.ResolveSeeds(null, out var defaultTag));
            Assert.AreEqual("held-out", defaultTag);
            Assert.AreEqual(EvalProtocol.HeldOutSeeds, EvalProtocol.ResolveSeeds("held-out", out _));

            Assert.AreEqual(new[] { EvalProtocol.TrainingRunSeed }, EvalProtocol.ResolveSeeds("train", out var trainTag));
            Assert.AreEqual("train", trainTag);

            Assert.AreEqual(new[] { 7, 42, 99 }, EvalProtocol.ResolveSeeds("7, 42,99", out var customTag));
            Assert.AreEqual("custom", customTag);
        }

        [Test]
        public void HeldOutSeeds_AreTwentyDistinctAndDisjointFromTraining()
        {
            Assert.AreEqual(20, EvalProtocol.HeldOutSeeds.Length);
            Assert.AreEqual(20, EvalProtocol.HeldOutSeeds.Distinct().Count());
            Assert.IsTrue(EvalProtocol.HeldOutSeeds.All(s => s >= 1001 && s <= 1020));
            Assert.IsFalse(EvalProtocol.HeldOutSeeds.Contains(EvalProtocol.TrainingRunSeed),
                "held-out seeds must stay disjoint from the training seed");
        }

        [Test]
        public void WilsonLowerBound_MatchesKnownValues()
        {
            Assert.AreEqual(0.5313f, EvalProtocol.WilsonLowerBound(15, 20), 1e-3f);
            Assert.AreEqual(0.8389f, EvalProtocol.WilsonLowerBound(20, 20), 1e-3f);
            Assert.AreEqual(0f, EvalProtocol.WilsonLowerBound(0, 20), 1e-6f);
            Assert.AreEqual(0f, EvalProtocol.WilsonLowerBound(0, 0), 1e-6f, "no trials must yield a zero bound");
        }

        [Test]
        public void WilsonLowerBound_TightensWithSampleSizeAtFixedRate()
        {
            Assert.Less(EvalProtocol.WilsonLowerBound(10, 20), EvalProtocol.WilsonLowerBound(100, 200));
            Assert.Less(EvalProtocol.WilsonLowerBound(100, 200), 0.5f);
        }
    }
}
#endif
