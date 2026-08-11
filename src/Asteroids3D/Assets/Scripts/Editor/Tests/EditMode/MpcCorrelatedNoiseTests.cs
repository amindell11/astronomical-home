#if UNITY_EDITOR
using Movement.MPC;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Tests.EditMode
{
    /// <summary>
    /// Sampler-level tests for the knot-interpolated (time-correlated) exploration noise
    /// in <see cref="GenerateCandidatesJob"/>. i.i.d. per-step Gaussian noise almost never
    /// holds one control sign long enough to express a sustained maneuver; knot-based noise
    /// must, by construction. These drive the job directly with a fixed seed — no ship,
    /// solver, or assets.
    /// </summary>
    [Category("MPC")]
    public class MpcCorrelatedNoiseTests
    {
        private const int Horizon = 17;    // matches live horizonSeconds 1.7 / rolloutDt 0.1
        private const int Samples = 256;
        private const int NoiseKnots = 5;
        private const float NoiseStd = 0.75f;
        private const uint Seed = 12345;

        /// <summary>Runs the candidate-generation job over a zero warm start and hands the
        /// candidate buffer to the assertion. Zero warm start means candidate values ARE the
        /// (clamped) noise, so sign structure is directly observable.</summary>
        private static void WithCandidates(System.Action<NativeArray<Control>> assert)
        {
            var warmStart = new NativeArray<Control>(Horizon, Allocator.TempJob);
            var candidates = new NativeArray<Control>(Samples * Horizon, Allocator.TempJob);
            try
            {
                new GenerateCandidatesJob
                {
                    warmStart = warmStart,
                    candidates = candidates,
                    horizon = Horizon,
                    noiseStd = NoiseStd,
                    noiseKnots = NoiseKnots,
                    rngSeed = Seed,
                }.Schedule(Samples, 1).Complete();

                assert(candidates);
            }
            finally
            {
                warmStart.Dispose();
                candidates.Dispose();
            }
        }

        private static int LongestSameSignRun(NativeArray<Control> candidates, int candidate, int channel)
        {
            var longest = 0;
            var run = 0;
            var prevSign = 0;
            for (var j = 0; j < Horizon; j++)
            {
                var v = Channel(candidates[candidate * Horizon + j], channel);
                var sign = v > 0f ? 1 : (v < 0f ? -1 : 0);
                run = (sign != 0 && sign == prevSign) ? run + 1 : 1;
                if (run > longest) longest = run;
                prevSign = sign;
            }
            return longest;
        }

        private static float Channel(Control u, int channel) =>
            channel == 0 ? u.thrust : channel == 1 ? u.strafe : u.yawTorque;

        [Test]
        public void KnotNoise_HoldsStrafeSignForFivePlusSteps_AtMeaningfulProbability()
        {
            WithCandidates(candidates =>
            {
                // Candidate 0 is the pure warm start — skip it.
                var held = 0;
                for (var i = 1; i < Samples; i++)
                    if (LongestSameSignRun(candidates, i, 1) >= 5) held++;

                var fraction = held / (float)(Samples - 1);
                // With 5 knots over 17 steps (segment length 4), a >=5-step hold needs two
                // adjacent knots of equal sign: P >= 1 - 0.5^4 ~= 0.94 per candidate.
                // Assert well below that to stay robust to seed choice.
                Assert.That(fraction, Is.GreaterThan(0.5f),
                    $"Only {fraction:P0} of candidates held strafe sign for >=5 consecutive steps; " +
                    "correlated noise should make sustained maneuvers common, not rare.");
            });
        }

        [Test]
        public void KnotNoise_SignFlipsBoundedByKnotCount()
        {
            WithCandidates(candidates =>
            {
                // Linear interpolation between knots is monotonic per segment, so each of the
                // (knots - 1) segments can flip a channel's sign at most once.
                const int maxFlips = NoiseKnots - 1;
                for (var i = 1; i < Samples; i++)
                for (var ch = 0; ch < 3; ch++)
                {
                    var flips = 0;
                    var prevSign = 0;
                    for (var j = 0; j < Horizon; j++)
                    {
                        var v = Channel(candidates[i * Horizon + j], ch);
                        var sign = v > 0f ? 1 : (v < 0f ? -1 : 0);
                        if (sign != 0 && prevSign != 0 && sign != prevSign) flips++;
                        if (sign != 0) prevSign = sign;
                    }
                    Assert.That(flips, Is.LessThanOrEqualTo(maxFlips),
                        $"Candidate {i} channel {ch} flipped sign {flips} times — noise is not knot-correlated.");
                }
            });
        }

        [Test]
        public void Candidate0_IsPureWarmStart()
        {
            WithCandidates(candidates =>
            {
                for (var j = 0; j < Horizon; j++)
                {
                    Assert.That(candidates[j].thrust, Is.Zero);
                    Assert.That(candidates[j].strafe, Is.Zero);
                    Assert.That(candidates[j].yawTorque, Is.Zero);
                }
            });
        }
    }
}
#endif
