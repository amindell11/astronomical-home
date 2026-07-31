#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Game.RLHarness;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the frozen eval protocol: held-out seed set shape/disjointness, seed-selection resolution (train vs held-out vs explicit), the Wilson 95% lower-bound math the arc gate reads, and the caller-owned artifact-dir override the eval gate names.</summary>
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

        [Test]
        public void NewRunPath_HonorsDirOverrideSoTheCallerReadsBackWhatItNamed()
        {
            var dir = Path.Combine(Path.GetTempPath(), "rl-eval-out-" + Path.GetRandomFileName());
            try
            {
                var path = EpisodeJsonl.NewRunPath("held-out", CheckpointEvaluator.ResultsFolder, dir);
                Assert.AreEqual(dir, Path.GetDirectoryName(path));
                Assert.IsTrue(Directory.Exists(dir));
                Assert.IsTrue(Path.GetFileName(path).EndsWith("-held-out.jsonl"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void EvaluatorSummarize_AggregatesPerOpponentWithoutABlend()
        {
            var win = EpisodeOutcome.Win.ToString();
            var loss = EpisodeOutcome.Loss.ToString();
            var draw = EpisodeOutcome.Draw.ToString();
            var summaries = CheckpointEvaluator.Summarize(new[]
            {
                ("Aggressor", win), ("Dummy", win),
                ("Aggressor", loss), ("Dummy", win),
                ("Aggressor", draw), ("Dummy", win),
            });

            Assert.AreEqual(2, summaries.Length, "one entry per opponent, nothing blended");
            Assert.AreEqual("Aggressor", summaries[0].opponent);
            Assert.AreEqual(3, summaries[0].episodes);
            Assert.AreEqual(1, summaries[0].wins);
            Assert.AreEqual(1, summaries[0].losses);
            Assert.AreEqual(1, summaries[0].draws, "draws are non-wins, tallied separately");
            Assert.AreEqual(1f / 3f, summaries[0].winRate, 1e-6f);
            Assert.AreEqual(EvalProtocol.WilsonLowerBound(1, 3), summaries[0].wilsonLowerBound95, 1e-6f);

            Assert.AreEqual("Dummy", summaries[1].opponent);
            Assert.AreEqual(3, summaries[1].wins);
            Assert.AreEqual(1f, summaries[1].winRate, 1e-6f);
            Assert.AreEqual(EvalProtocol.WilsonLowerBound(3, 3), summaries[1].wilsonLowerBound95, 1e-6f);
        }

        [Test]
        public void EvalSummarySchema_IsV2SoAPreRenameReaderCannotSilentlyMisparseIt()
        {
            Assert.AreEqual("rl-eval-summary-v2", CheckpointEvaluator.SchemaId);
            var json = JsonUtility.ToJson(new CheckpointEvaluator.Summary { schema = CheckpointEvaluator.SchemaId });
            StringAssert.Contains("\"opponents\"", json, "eval_gate.read_score reads opponents[], not archetypes[]");
            StringAssert.Contains("\"probes\"", json, "probe sidecars replaced the embedded behavior[] blocks");
        }
    }
}
#endif
