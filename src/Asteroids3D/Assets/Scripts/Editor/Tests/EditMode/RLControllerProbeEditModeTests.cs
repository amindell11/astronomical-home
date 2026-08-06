#if UNITY_EDITOR
using Game.RLHarness;
using Movement.MPC;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the controller-probe sampler's math on synthetic samples: deadband-hysteresis reversal counting beside the strict rule, anchor-rate wrapping across ±π with gap breaks, obstacle-threat/clear split accounting, and the classification's agreement with Cost.ObstacleCosts semantics.</summary>
    [Category("AI")]
    public class RLControllerProbeEditModeTests
    {
        private const float Dt = 0.02f;

        private static ControllerSampler Sampler() =>
            new(ControllerProbe.DefaultTorqueDeadband, ControllerProbe.DefaultYawRateDeadbandDegPerSec);

        private static void Step(ControllerSampler sampler, float torque = 0f, float yawRate = 0f,
            bool threat = false, float dt = Dt) =>
            sampler.Sample(torque, yawRate, hasAnchor: false, anchorYawRad: 0f, threat, dt);

        private static void Anchor(ControllerSampler sampler, float anchorYawRad, float dt = Dt) =>
            sampler.Sample(0f, 0f, hasAnchor: true, anchorYawRad, underThreat: false, dt);

        [Test]
        public void DeadbandFlip_CountsOnlyOnceTheNewSideClearsTheThreshold()
        {
            var sampler = Sampler();

            Step(sampler, torque: 0.5f);
            Step(sampler, torque: -0.05f);
            Step(sampler, torque: 0.05f);
            Step(sampler, torque: -0.5f);

            var row = sampler.ToRow(new EpisodeResult { simSeconds = 1f }, "Aggressor");
            Assert.AreEqual(3f, row.torqueReversalsPerSec, 1e-5f, "strict rule counts every sign alternation");
            Assert.AreEqual(1f, row.torqueDeadbandReversalsPerSec, 1e-5f,
                "sub-deadband wiggles must not count until the new side clears 0.1");
        }

        [Test]
        public void DeadbandFlip_ZeroKeepsSign_AndExactThresholdStaysInside()
        {
            var sampler = Sampler();

            Step(sampler, torque: 0.5f, yawRate: 30f);
            Step(sampler, torque: 0f, yawRate: 0f);
            Step(sampler, torque: -0.1f, yawRate: -10f);
            Step(sampler, torque: -0.5f, yawRate: -30f);

            var row = sampler.ToRow(new EpisodeResult { simSeconds = 1f }, "Aggressor");
            Assert.AreEqual(1f, row.torqueDeadbandReversalsPerSec, 1e-5f,
                "zero keeps the armed sign and an exact-threshold value stays inside the deadband");
            Assert.AreEqual(1f, row.noseDeadbandReversalsPerSec, 1e-5f);
            Assert.AreEqual(1f, row.torqueReversalsPerSec, 1e-5f, "strict: zero is skipped, − counted once");
            Assert.AreEqual(1f, row.noseReversalsPerSec, 1e-5f);
        }

        [Test]
        public void AnchorRate_WrapsAcrossPi()
        {
            var sampler = Sampler();

            Anchor(sampler, 3.1f, dt: 0.1f);
            Anchor(sampler, -3.1f, dt: 0.1f);

            var row = sampler.ToRow(new EpisodeResult { simSeconds = 0.2f }, "Orbiter");
            Assert.AreEqual(1, row.anchorSamples);
            var expected = (2f * Mathf.PI - 6.2f) / 0.1f * Mathf.Rad2Deg;
            Assert.AreEqual(expected, row.meanAnchorRateDegPerSec, 1e-2f,
                "an unwrapped delta would read ~3552 deg/s");
            Assert.AreEqual(expected, row.p90AnchorRateDegPerSec, 1e-2f);
        }

        [Test]
        public void AnchorRate_EmitsNoSampleAcrossAnAnchorlessGap()
        {
            var sampler = Sampler();

            Anchor(sampler, 1.0f);
            Step(sampler);
            Anchor(sampler, 2.0f);
            Anchor(sampler, 2.1f);

            var row = sampler.ToRow(new EpisodeResult { simSeconds = 1f }, "Dummy");
            Assert.AreEqual(1, row.anchorSamples,
                "the 1.0→2.0 delta spans the anchorless tick and must not emit; only 2.0→2.1 samples");
            Assert.AreEqual(0.1f / Dt * Mathf.Rad2Deg, row.meanAnchorRateDegPerSec, 1e-2f);
        }

        [Test]
        public void ThreatSplit_AttributesStepsAndFlipsToTheCurrentBucket()
        {
            var sampler = Sampler();

            Step(sampler, torque: 0.5f, yawRate: 20f, threat: true, dt: 0.25f);
            Step(sampler, torque: -0.5f, yawRate: 20f, threat: true, dt: 0.25f);
            Step(sampler, torque: 0.5f, yawRate: -20f, threat: false, dt: 0.25f);
            Step(sampler, torque: 0.5f, yawRate: 20f, threat: false, dt: 0.25f);

            var row = sampler.ToRow(new EpisodeResult { simSeconds = 1f }, "Aggressor");
            Assert.AreEqual(0.5f, row.threatStepFraction, 1e-5f);
            Assert.AreEqual(2, row.threat.steps);
            Assert.AreEqual(2, row.clear.steps);
            Assert.AreEqual(2f, row.threat.torqueReversalsPerSec, 1e-5f, "1 flip over the 0.5 threat-seconds");
            Assert.AreEqual(2f, row.threat.torqueDeadbandReversalsPerSec, 1e-5f);
            Assert.AreEqual(0f, row.threat.noseReversalsPerSec, 1e-5f);
            Assert.AreEqual(2f, row.clear.torqueReversalsPerSec, 1e-5f);
            Assert.AreEqual(4f, row.clear.noseReversalsPerSec, 1e-5f, "2 flips over the 0.5 clear-seconds");
            Assert.AreEqual(4f, row.clear.noseDeadbandReversalsPerSec, 1e-5f);
            Assert.AreEqual(0.5f, row.meanAbsYawTorque, 1e-5f);
            Assert.AreEqual(20f, row.meanAbsYawRateDegPerSec, 1e-5f);
            Assert.AreEqual(2f, row.torqueReversalsPerSec, 1e-5f, "overall = threat + clear over simSeconds");
            Assert.AreEqual(2f, row.noseReversalsPerSec, 1e-5f);
        }

        [Test]
        public void ObstacleThreat_AgreesWithObstacleCostsSemanticsAcrossAllThreeRegimes()
        {
            var cfg = new Config
            {
                shipRadius = 0.5f,
                collisionSafetyMargin = 0.1f,
                maxLatAccel = 1f,
                wObstacle = 1f,
                collisionPenalty = 100f,
            };
            const float profileScale = 1f;
            var hullRadius = cfg.shipRadius * profileScale + cfg.collisionSafetyMargin;
            var state = new State { pos = float2.zero, vel = new float2(0f, 10f) };

            var regimes = new (float2 obstaclePos, bool expected, string label)[]
            {
                (new float2(0f, 0.5f), true, "hull overlap ahead"),
                (new float2(0f, 5f), true, "collision course without overlap"),
                (new float2(0f, -5f), false, "clear (behind the velocity)"),
            };

            foreach (var (obstaclePos, expected, label) in regimes)
            {
                var obstacles = new NativeArray<ObstacleData>(1, Allocator.Temp);
                obstacles[0] = new ObstacleData { position = obstaclePos, radius = 1f };
                var input = new CostInput { obstacles = obstacles, obstacleCount = 1 };

                Cost.ObstacleCosts(state, input, cfg, profileScale, out var collision, out var turnAway);
                var costsFired = collision > 0f || turnAway > 0f;
                var classified = ControllerProbe.ObstacleThreat(state.pos, state.vel, obstacles, 1,
                    hullRadius, cfg.maxLatAccel);

                Assert.AreEqual(expected, costsFired, $"{label}: ObstacleCosts semantics moved under the probe");
                Assert.AreEqual(costsFired, classified,
                    $"{label}: the probe's classification must agree with ObstacleCosts");
                obstacles.Dispose();
            }
        }

        [Test]
        public void ZeroStepEpisode_ZeroFills()
        {
            var row = Sampler().ToRow(new EpisodeResult { simSeconds = 0f }, "Dummy");

            Assert.AreEqual(0, row.steps);
            Assert.AreEqual(0, row.anchorSamples);
            Assert.AreEqual(0f, row.meanAbsYawTorque);
            Assert.AreEqual(0f, row.torqueReversalsPerSec);
            Assert.AreEqual(0f, row.meanAnchorRateDegPerSec);
            Assert.AreEqual(0f, row.threatStepFraction);
            Assert.AreEqual(0f, row.threat.meanAbsYawRateDegPerSec);
        }

        [Test]
        public void Summary_PoolsAnchorSamplesAcrossEpisodesInsteadOfAveragingEpisodeStats()
        {
            var pool = new ControllerPool();

            var first = Sampler();
            Anchor(first, 0f, dt: 1f);
            Anchor(first, 100f * Mathf.Deg2Rad, dt: 1f);
            first.DrainInto(pool, new EpisodeResult { simSeconds = 2f });

            var second = Sampler();
            Anchor(second, 0f, dt: 1f);
            for (var i = 1; i <= 3; i++)
                Anchor(second, i * 10f * Mathf.Deg2Rad, dt: 1f);
            second.DrainInto(pool, new EpisodeResult { simSeconds = 4f });

            var summary = ControllerSummary.Summarize("Evader", pool);
            Assert.AreEqual("Evader", summary.opponent);
            Assert.AreEqual(2, summary.episodes);
            Assert.AreEqual(3f, summary.meanEpisodeSeconds, 1e-5f);
            Assert.AreEqual(4, summary.anchorSamples);
            Assert.AreEqual(32.5f, summary.meanAnchorRateDegPerSec, 1e-3f,
                "pooled mean over {100,10,10,10}; mean-of-episode-means would say 55");
            Assert.AreEqual(100f, summary.p90AnchorRateDegPerSec, 1e-3f, "pooled p90, never averaged");
        }
    }
}
#endif
