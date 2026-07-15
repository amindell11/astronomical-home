#if UNITY_EDITOR
using System.Linq;
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the frozen eval protocol: held-out seed set shape/disjointness and the Wilson 95% lower-bound math the arc gate reads.</summary>
    [Category("AI")]
    public class RLEvalProtocolEditModeTests
    {
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
