#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using Game.RLHarness;
using Movement;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the probe clients' math on synthetic samples: strict-sign reversal counting, pooled percentiles (never mean-of-means), and the per-opponent aggregate shapes.</summary>
    [Category("AI")]
    public class RLProbeEditModeTests
    {
        private sealed class FakeReadout : IPolicyReadout
        {
            private readonly List<PolicyAction> actions = new();

            public int Count => actions.Count;
            public int TotalDecisions => actions.Count;
            public PolicyAction ActionFromNewest(int index) => actions[actions.Count - 1 - index];

            public void Decide(float cmdDeg, float weight) =>
                actions.Add(new PolicyAction(cmdDeg * Mathf.Deg2Rad, weight, 0f, 0f, 0f));

            public void DecideSentence(float aimW, float posW, float velW, float laneW, float fieldW,
                int aimRef = 0, int posRef = 0, int velRef = 0) =>
                actions.Add(new PolicyAction(0f, aimW, 0f, 0f, velW, 0f, 0f, 0f, posW, laneW, fieldW,
                    aimRef, posRef, velRef, 0, 0, false, false, false));
        }

        private static Kinematics Kin(float yaw, float yawRate) =>
            new(Vector2.zero, Vector2.zero, yaw, yawRate, 0f);

        [Test]
        public void Percentile_IsCeilRankOnASortedCopy()
        {
            var samples = new List<float> { 10f, 1f, 5f, 3f, 8f, 2f, 9f, 4f, 7f, 6f };

            Assert.AreEqual(5f, FacingSummary.Percentile(samples, 50), "p50 of 1..10 = ceil(5) → 5th element");
            Assert.AreEqual(9f, FacingSummary.Percentile(samples, 90));
            Assert.AreEqual(1f, FacingSummary.Percentile(samples, 10));
            Assert.AreEqual(4f, FacingSummary.Percentile(new List<float> { 4f }, 90), "one sample is every percentile");
            Assert.AreEqual(0f, FacingSummary.Percentile(new List<float>(), 50), "an empty pool zero-fills");
            Assert.AreEqual(10f, samples[0], "the input list must not be reordered");
        }

        [Test]
        public void FacingSampler_CountsStrictSignFlipsAndPoolsPerDecisionSamples()
        {
            var readout = new FakeReadout();
            var sampler = new FacingSampler(readout);

            sampler.Sample(Kin(yaw: 0f, yawRate: 30f), anchorYawRad: 0f);

            readout.Decide(cmdDeg: 90f, weight: 1f);
            sampler.Sample(Kin(yaw: 10f, yawRate: -30f), anchorYawRad: 0f);

            readout.Decide(cmdDeg: 30f, weight: 0.1f);
            sampler.Sample(Kin(yaw: 20f, yawRate: 0f), anchorYawRad: 0f);

            readout.Decide(cmdDeg: 80f, weight: 0.5f);
            sampler.Sample(Kin(yaw: 30f, yawRate: 40f), anchorYawRad: 0f);

            var result = new EpisodeResult { simSeconds = 2f, outcome = "Win" };
            var row = sampler.ToRow(in result, "Aggressor", authorityScale: 1f);

            Assert.AreEqual("Aggressor", row.opponent);
            Assert.AreEqual(3, row.decisions);
            Assert.AreEqual(1f, row.noseReversalsPerSec, 1e-5f, "2 nose flips over 2 sim-seconds");
            Assert.AreEqual(0.5f, row.cmdReversalsPerSec, 1e-5f, "1 command alternation over 2 sim-seconds");
            Assert.AreEqual(25f, row.meanAbsYawRateDegPerSec, 1e-4f);
            Assert.AreEqual(140f / 3f, row.meanFacingErrorDeg, 1e-3f);
            Assert.AreEqual(55f, row.meanCmdDeltaDeg, 1e-4f);
            Assert.AreEqual(50f, row.medianCmdDeltaDeg, 1e-4f);
            Assert.AreEqual(1f / 3f, row.lowAuthorityFraction, 1e-5f, "one decision under the 0.2 threshold");
        }

        [Test]
        public void FacingSampler_ResolvesOffsetAgainstTheCurrentAnchor()
        {
            var readout = new FakeReadout();
            var sampler = new FacingSampler(readout);

            readout.Decide(cmdDeg: 0f, weight: 1f);
            sampler.Sample(Kin(yaw: 90f, yawRate: 0f), anchorYawRad: 90f * Mathf.Deg2Rad);
            sampler.Sample(Kin(yaw: -90f, yawRate: 0f), anchorYawRad: -90f * Mathf.Deg2Rad);

            readout.Decide(cmdDeg: 30f, weight: 1f);
            sampler.Sample(Kin(yaw: -60f, yawRate: 0f), anchorYawRad: -90f * Mathf.Deg2Rad);

            var result = new EpisodeResult { simSeconds = 1f };
            var row = sampler.ToRow(in result, "Orbiter", authorityScale: 1f);

            Assert.AreEqual(0f, row.meanFacingErrorDeg, 1e-4f,
                "anchor motion must re-resolve facing between policy decisions");
            Assert.AreEqual(30f, row.meanCmdDeltaDeg, 1e-4f,
                "command churn measures policy offsets, not anchor motion");
        }

        [Test]
        public void FacingSampler_ZeroDecisionEpisodeZeroFills()
        {
            var sampler = new FacingSampler(new FakeReadout());
            var result = new EpisodeResult { simSeconds = 0f };

            var row = sampler.ToRow(in result, "Dummy", authorityScale: 1f);

            Assert.AreEqual(0, row.decisions);
            Assert.AreEqual(0f, row.meanFacingErrorDeg);
            Assert.AreEqual(0f, row.noseReversalsPerSec);
            Assert.AreEqual(0f, row.meanAbsYawRateDegPerSec);
        }

        [Test]
        public void FacingSummary_PoolsSamplesAcrossEpisodesInsteadOfAveragingEpisodeStats()
        {
            var readout = new FakeReadout();
            var pool = new FacingPool();

            var first = new FacingSampler(readout);
            readout.Decide(cmdDeg: 0f, weight: 1f);
            first.Sample(Kin(yaw: 100f, yawRate: 0f), anchorYawRad: 0f);
            var resultA = new EpisodeResult { simSeconds = 1f };
            first.DrainInto(pool, in resultA);

            var second = new FacingSampler(readout);
            readout.Decide(cmdDeg: 0f, weight: 1f);
            second.Sample(Kin(yaw: 10f, yawRate: 0f), anchorYawRad: 0f);
            second.Sample(Kin(yaw: 20f, yawRate: 0f), anchorYawRad: 0f);
            second.Sample(Kin(yaw: 30f, yawRate: 0f), anchorYawRad: 0f);
            var resultB = new EpisodeResult { simSeconds = 3f };
            second.DrainInto(pool, in resultB);

            var summary = FacingSummary.Summarize("Evader", pool, authorityScale: 2f);

            Assert.AreEqual("Evader", summary.opponent);
            Assert.AreEqual(2, summary.episodes);
            Assert.AreEqual(2, summary.decisions);
            Assert.AreEqual(2f, summary.meanEpisodeSeconds);
            Assert.AreEqual(2f, summary.authorityScale);
            Assert.AreEqual(20f, summary.medianFacingErrorDeg,
                "pooled p50 over {100,10,20,30} = 20; mean-of-episode-medians would say 60");
            Assert.AreEqual(40f, summary.meanFacingErrorDeg, 1e-4f);
        }

        [Test]
        public void ContactSummary_AggregatesPerOpponentAtTheBenchShape()
        {
            var rows = new List<ContactRow>
            {
                new()
                {
                    longestContactSec = 2f, contactFraction = 0.5f, minRange = 1f, simSeconds = 10f,
                    mutualKill = true, agentHpLostPct = 0.4f, oppHpLostPct = 1f, meanTumbleInContact = 30f,
                },
                new()
                {
                    longestContactSec = 4f, contactFraction = 0.3f, minRange = 3f, simSeconds = 20f,
                    mutualKill = false, agentHpLostPct = 0.2f, oppHpLostPct = 0.6f, meanTumbleInContact = 10f,
                },
            };

            var summary = ContactSummary.Summarize("Aggressor", rows);

            Assert.AreEqual(ContactSummary.SchemaId, summary.schema);
            Assert.AreEqual("Aggressor", summary.opponent);
            Assert.AreEqual(2, summary.episodes);
            Assert.AreEqual(3f, summary.meanLongestContactSec);
            Assert.AreEqual(4f, summary.maxLongestContactSec);
            Assert.AreEqual(0.4f, summary.meanContactFraction, 1e-5f);
            Assert.AreEqual(2f, summary.meanMinRange);
            Assert.AreEqual(15f, summary.meanTtk);
            Assert.AreEqual(0.5f, summary.mutualKillRate);
            Assert.AreEqual(0.3f, summary.meanAgentHpLostPct, 1e-5f);
            Assert.AreEqual(0.8f, summary.meanOppHpLostPct, 1e-5f);
            Assert.AreEqual(20f, summary.meanTumbleInContact);
        }

        [Test]
        public void SentenceSampler_CountsReferentSwitchesAndPoolsWeights()
        {
            var readout = new FakeReadout();
            var sampler = new SentenceSampler(readout);

            sampler.Sample();   // no decision yet: nothing to record

            readout.DecideSentence(aimW: 1f, posW: -0.5f, velW: 0.5f, laneW: 0f, fieldW: 1f, aimRef: 0, posRef: 2);
            sampler.Sample();
            sampler.Sample();   // same decision seen twice: only the first records

            readout.DecideSentence(aimW: 0.5f, posW: 0.5f, velW: 0.5f, laneW: -1f, fieldW: 0f, aimRef: 1, posRef: 2);
            sampler.Sample();

            readout.DecideSentence(aimW: 0f, posW: 0f, velW: 1f, laneW: 0f, fieldW: 0f, aimRef: 1, posRef: 0);
            sampler.Sample();

            var result = new EpisodeResult { simSeconds = 2f, outcome = "Win" };
            var row = sampler.ToRow(in result, "Aggressor");

            Assert.AreEqual(SentenceProbeRow.SchemaId, row.schema);
            Assert.AreEqual(3, row.decisions);
            Assert.AreEqual(1f / 3f, row.aimReferentSwitchesPerDecision, 1e-5f, "0→1 once over 3 decisions");
            Assert.AreEqual(1f / 3f, row.posReferentSwitchesPerDecision, 1e-5f, "2→0 once");
            Assert.AreEqual(0f, row.velReferentSwitchesPerDecision, 1e-5f, "VEL never moved off the enemy");
            Assert.AreEqual(1f, row.rockBoundFraction, 1e-5f, "every decision bound at least one rock slot");
            Assert.AreEqual(0.5f, row.meanAimWeight, 1e-5f);
            Assert.AreEqual(1f / 3f, row.meanAbsPosWeight, 1e-5f, "signed POS weights pool as magnitude");
            Assert.AreEqual(2f / 3f, row.meanVelWeight, 1e-5f);
            Assert.AreEqual(1f / 3f, row.meanAbsLaneWeight, 1e-5f);
            Assert.AreEqual(1f / 3f, row.meanFieldWeight, 1e-5f);
            Assert.Greater(row.meanWeightEntropy, 0f);
        }

        [Test]
        public void SentenceWeightEntropy_ZeroWhenSaturated_MaxWhenUniform()
        {
            var saturated = new PolicyAction(0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0, 0, 0, 0, 0, false, false, false);
            Assert.AreEqual(0f, SentenceSampler.WeightEntropy(in saturated), 1e-5f,
                "all authority in one slot = zero entropy (the saturation signal)");

            var uniform = new PolicyAction(0f, 0.5f, 0f, 0f, 0.5f, 0f, 0f, 0f, -0.5f, 0.5f, 0.5f,
                0, 0, 0, 0, 0, false, false, false);
            Assert.AreEqual(Mathf.Log(5f), SentenceSampler.WeightEntropy(in uniform), 1e-4f,
                "equal |weights| across the five slots = ln 5, sign ignored");

            var silent = new PolicyAction(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                0, 0, 0, 0, 0, false, false, false);
            Assert.AreEqual(0f, SentenceSampler.WeightEntropy(in silent), "an all-zero sentence zero-fills");
        }

        [Test]
        public void SentenceSampler_ZeroDecisionEpisodeZeroFills()
        {
            var sampler = new SentenceSampler(new FakeReadout());
            var row = sampler.ToRow(new EpisodeResult { simSeconds = 0f }, "Dummy");
            Assert.AreEqual(0, row.decisions);
            Assert.AreEqual(0f, row.aimReferentSwitchesPerDecision);
            Assert.AreEqual(0f, row.meanWeightEntropy);
        }

        [Test]
        public void SentenceSummary_PoolsDecisionsAcrossEpisodes()
        {
            var readout = new FakeReadout();
            var pool = new SentencePool();

            var first = new SentenceSampler(readout);
            readout.DecideSentence(1f, 0f, 0f, 0f, 0f, aimRef: 1);
            first.Sample();
            first.DrainInto(pool);

            var second = new SentenceSampler(readout);
            readout.DecideSentence(0f, 0f, 1f, 0f, 0f);
            second.Sample();
            readout.DecideSentence(0f, 0f, 0f, 0f, 0f, velRef: 3);
            second.Sample();
            second.DrainInto(pool);

            var summary = SentenceProbeSummary.Summarize("Evader", pool);

            Assert.AreEqual(SentenceProbeSummary.SchemaId, summary.schema);
            Assert.AreEqual(2, summary.episodes);
            Assert.AreEqual(3, summary.decisions);
            Assert.AreEqual(1f / 3f, summary.velReferentSwitchesPerDecision, 1e-5f,
                "switch counts pool over decisions, never average per-episode rates");
            Assert.AreEqual(2f / 3f, summary.rockBoundFraction, 1e-5f);
            Assert.AreEqual(1f / 3f, summary.meanAimWeight, 1e-5f);
        }

        [Test]
        public void ContactSummary_ZeroEpisodesZeroFill()
        {
            var summary = ContactSummary.Summarize("Dummy", new List<ContactRow>());

            Assert.AreEqual(0, summary.episodes);
            Assert.AreEqual(0f, summary.meanLongestContactSec);
            Assert.AreEqual(0f, summary.mutualKillRate);
        }
    }
}
#endif
