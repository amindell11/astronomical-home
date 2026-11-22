#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using Harness.Episodes;
using Harness.Hosts;
using Harness.Hosts.Lanes;

namespace Tests.EditMode
{
    /// <summary>Pins the harness seed protocol: held-out seed set shape/disjointness, seed-selection resolution (train vs held-out vs explicit), and the caller-owned artifact-dir override the lane launcher names.</summary>
    [Category("AI")]
    public class HarnessSeedsEditModeTests
    {
        [Test]
        public void ResolveSeeds_DefaultsToHeldOutAndDistinguishesTags()
        {
            Assert.AreEqual(HarnessSeeds.HeldOutSeeds, HarnessSeeds.ResolveSeeds(null, out var defaultTag));
            Assert.AreEqual("held-out", defaultTag);
            Assert.AreEqual(HarnessSeeds.HeldOutSeeds, HarnessSeeds.ResolveSeeds("held-out", out _));

            Assert.AreEqual(new[] { HarnessSeeds.TrainingRunSeed }, HarnessSeeds.ResolveSeeds("train", out var trainTag));
            Assert.AreEqual("train", trainTag);

            Assert.AreEqual(new[] { 7, 42, 99 }, HarnessSeeds.ResolveSeeds("7, 42,99", out var customTag));
            Assert.AreEqual("custom", customTag);
        }

        [Test]
        public void HeldOutSeeds_AreTwentyDistinctAndDisjointFromTraining()
        {
            Assert.AreEqual(20, HarnessSeeds.HeldOutSeeds.Length);
            Assert.AreEqual(20, HarnessSeeds.HeldOutSeeds.Distinct().Count());
            Assert.IsTrue(HarnessSeeds.HeldOutSeeds.All(s => s >= 1001 && s <= 1020));
            Assert.IsFalse(HarnessSeeds.HeldOutSeeds.Contains(HarnessSeeds.TrainingRunSeed),
                "held-out seeds must stay disjoint from the training seed");
        }

        [Test]
        public void NewRunPath_HonorsDirOverrideSoTheCallerReadsBackWhatItNamed()
        {
            var dir = Path.Combine(Path.GetTempPath(), "rl-eval-out-" + Path.GetRandomFileName());
            try
            {
                var path = EpisodeJsonl.NewRunPath("held-out", MatrixLane.ResultsFolder, dir);
                Assert.AreEqual(dir, Path.GetDirectoryName(path));
                Assert.IsTrue(Directory.Exists(dir));
                Assert.IsTrue(Path.GetFileName(path).EndsWith("-held-out.jsonl"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }
    }
}
#endif
